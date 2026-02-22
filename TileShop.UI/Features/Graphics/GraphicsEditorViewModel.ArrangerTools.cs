using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using Monaco.PathTree;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics;

public partial class GraphicsEditorViewModel
{
    [ObservableProperty] private bool _isPencilDrawing;
    [ObservableProperty] private PixelTool _activePixelTool = PixelTool.Pencil;
    [ObservableProperty] private ArrangerTool _activeArrangerTool = ArrangerTool.Select;
    [ObservableProperty] private bool _areSymmetryToolsEnabled;

    partial void OnActivePixelToolChanged(PixelTool oldValue, PixelTool newValue)
    {
        if (_pixelTools.TryGetValue(oldValue, out var outgoing))
        {
            var historyAction = outgoing.Deactivate(this);
            if (historyAction is not null)
                AddHistoryAction(historyAction);
        }
    }
    
    [RelayCommand]
    public void ChangeArrangerTool(ArrangerTool tool)
    {
        ActiveArrangerTool = tool;
    }

    [RelayCommand]
    public void ToggleSymmetryTools()
    {
        AreSymmetryToolsEnabled = !AreSymmetryToolsEnabled;
    }

    public void SetSelectToolMode() => ActiveArrangerTool = ArrangerTool.Select;
    public void SetApplyPaletteMode() => ActiveArrangerTool = ArrangerTool.ApplyPalette;

    [RelayCommand]
    public void ToggleGridlineVisibility()
    {
        GridSettings.ShowGridlines ^= true;
        OnImageModified?.Invoke();
    }

    [RelayCommand]
    public async Task ModifyGridSettings()
    {
        var model = new ModifyGridSettingsViewModel();
        _tracker.Track(model);
        var result = await _interactions.RequestAsync(model);

        if (result is not null)
        {
            GridSettings.WidthSpacing = result.WidthSpacing;
            GridSettings.HeightSpacing = result.HeightSpacing;
            GridSettings.ShiftX = result.ShiftX;
            GridSettings.ShiftY = result.ShiftY;
            GridSettings.PrimaryColor = result.PrimaryColor;
            GridSettings.SecondaryColor = result.SecondaryColor;
            GridSettings.LineColor = result.LineColor;

            GridSettings.AdjustGridlines(WorkingArranger);
            GridSettings.CreateBackgroundBrush();
            OnImageModified?.Invoke();

            _tracker.Persist(result);
        }
    }

    internal void TryApplyPalette(int pixelX, int pixelY, Palette palette)
    {
        if (!IsIndexedColor)
            return;

        bool needsRender = false;
        if (Selection.HasSelection && Selection.SelectionRect.ContainsPointSnapped(pixelX, pixelY))
        {
            int top = Selection.SelectionRect.SnappedTop / WorkingArranger.ElementPixelSize.Height;
            int bottom = Selection.SelectionRect.SnappedBottom / WorkingArranger.ElementPixelSize.Height;
            int left = Selection.SelectionRect.SnappedLeft / WorkingArranger.ElementPixelSize.Width;
            int right = Selection.SelectionRect.SnappedRight / WorkingArranger.ElementPixelSize.Width;

            for (int posY = top; posY < bottom; posY++)
            {
                for (int posX = left; posX < right; posX++)
                {
                    int elementX = posX * WorkingArranger.ElementPixelSize.Width;
                    int elementY = posY * WorkingArranger.ElementPixelSize.Height;
                    if (TryApplySinglePalette(elementX, elementY, palette, false))
                    {
                        needsRender = true;
                    }
                }
            }
        }
        else
        {
            if (TryApplySinglePalette(pixelX, pixelY, palette, true))
                needsRender = true;
        }

        if (needsRender)
            Render();
    }

    private bool TryApplySinglePalette(int pixelX, int pixelY, Palette palette, bool notify)
    {
        if (pixelX >= WorkingArranger.ArrangerPixelSize.Width || pixelY >= WorkingArranger.ArrangerPixelSize.Height)
            return false;

        var el = WorkingArranger.GetElementAtPixel(pixelX, pixelY);

        if (el is ArrangerElement { Codec: IIndexedCodec codec } element)
        {
            if (ReferenceEquals(palette, codec.Palette))
                return false;

            var result = _imageAdapter.TrySetPalette(pixelX, pixelY, palette);

            return result.Match(
                success =>
                {
                    Render();
                    IsModified = true;
                    return true;
                },
                fail => false);
        }
        return false;
    }

    public bool TryPickPalette(int pixelX, int pixelY)
    {
        if (!IsIndexedColor)
            return false;

        var elX = pixelX / WorkingArranger.ElementPixelSize.Width;
        var elY = pixelY / WorkingArranger.ElementPixelSize.Height;

        if (elX >= WorkingArranger.ArrangerElementSize.Width || elY >= WorkingArranger.ArrangerElementSize.Height)
            return false;

        var el = WorkingArranger.GetElement(elX, elY);

        if (el is ArrangerElement { Codec: IIndexedCodec codec })
        {
            SelectedPalette = Palettes.FirstOrDefault(x => ReferenceEquals(codec.Palette, x.Palette)) ??
                Palettes.FirstOrDefault(x => ReferenceEquals(_paletteStore.DefaultPalette, x.Palette));
        }

        return true;
    }

    [RelayCommand]
    public async Task AssociatePalette()
    {
        if (!IsIndexedColor)
            return;

        var projectTree = _projectService.GetContainingProject(Resource);
        var palettes = projectTree.EnumerateDepthFirst()
            .Where(x => x.Item is Palette)
            .Select(x => new AssociatePaletteModel((Palette)x.Item, projectTree.CreatePathKey(x)))
            .Concat(_paletteStore.GlobalPalettes.Select(x => new AssociatePaletteModel(x, x.Name)));

        var model = new AssociatePaletteViewModel(palettes);
        var dialogResult = await _interactions.RequestAsync(model);

        if (dialogResult is not null)
        {
            var palModel = new PaletteModel(dialogResult.SelectedPalette.Palette, dialogResult.SelectedPalette.Palette.Entries);
            Palettes.Add(palModel);
        }
    }

    [RelayCommand]
    public void ApplyPaste(ArrangerPaste paste)
    {
        var result = ApplyPasteInternal(paste);
        var message = result.Match(
            success =>
            {
                AddHistoryAction(new PasteArrangerHistoryAction(paste));
                IsModified = true;
                CancelOverlay();
                BitmapAdapter.Invalidate();
                OnImageModified?.Invoke();
                return new NotifyStatusMessage("Paste successfully applied");
            },
            fail => new NotifyStatusMessage(fail.Reason)
        );

        Messenger.Send(message);
    }

    private MagitekResult ApplyPasteInternal(ArrangerPaste paste)
    {
        if (paste.Copy is ElementCopy elementCopy)
        {
            return ApplyElementPaste(paste, elementCopy);
        }
        else
        {
            return ApplyPixelPaste(paste);
        }
    }

    private MagitekResult ApplyElementPaste(ArrangerPaste paste, ElementCopy elementCopy)
    {
        if (WorkingArranger is not ScatteredArranger arranger)
            return new MagitekResult.Failed($"Pasting elements into a '{WorkingArranger.GetType()}' is not supported");

        if (!_projectService.AreResourcesInSameProject(elementCopy.ProjectResource, OriginatingProjectResource!))
            return new MagitekResult.Failed("Copying arranger elements across projects is not permitted");

        var destRect = paste.Rect;
        var destElemWidth = WorkingArranger.ElementPixelSize.Width;
        var destElemHeight = WorkingArranger.ElementPixelSize.Height;

        int destX = Math.Max(0, destRect.SnappedLeft / destElemWidth);
        int destY = Math.Max(0, destRect.SnappedTop / destElemHeight);
        int sourceX = destRect.SnappedLeft / destElemWidth >= 0 ? 0 : -destRect.SnappedLeft / destElemWidth;
        int sourceY = destRect.SnappedTop / destElemHeight >= 0 ? 0 : -destRect.SnappedTop / destElemHeight;

        var destStart = new Point(destX, destY);
        var sourceStart = new Point(sourceX, sourceY);

        int copyWidth = Math.Min(elementCopy.Width - sourceX, WorkingArranger.ArrangerElementSize.Width - destX);
        int copyHeight = Math.Min(elementCopy.Height - sourceY, WorkingArranger.ArrangerElementSize.Height - destY);

        return ElementCopier.CopyElements(elementCopy, arranger, sourceStart, destStart, copyWidth, copyHeight);
    }

    private MagitekResult ApplyPixelPaste(ArrangerPaste paste)
    {
        int destX = Math.Max(0, paste.Rect.SnappedLeft);
        int destY = Math.Max(0, paste.Rect.SnappedTop);
        int sourceX = paste.Rect.SnappedLeft >= 0 ? 0 : -paste.Rect.SnappedLeft;
        int sourceY = paste.Rect.SnappedTop >= 0 ? 0 : -paste.Rect.SnappedTop;

        var destStart = new Point(destX, destY);
        var sourceStart = new Point(sourceX, sourceY);

        ArrangerCopy? copy = paste.Copy;

        if (paste.Copy is ElementCopy elementCopy)
            copy = elementCopy.ToPixelCopy();

        if (IsIndexedColor)
        {
            if (copy is IndexedPixelCopy indexedCopy)
            {
                int copyWidth = Math.Min(copy.Width - sourceX, _imageAdapter.Width - destX);
                int copyHeight = Math.Min(copy.Height - sourceY, _imageAdapter.Height - destY);

                // Need to get the underlying IndexedImage for ImageCopier
                return new MagitekResult.Failed("Pixel paste not yet supported in unified editor");
            }
            else if (copy is DirectPixelCopy)
            {
                return new MagitekResult.Failed("Direct->Indexed pasting is not yet implemented");
            }
        }
        else
        {
            if (copy is DirectPixelCopy directCopy)
            {
                int copyWidth = Math.Min(copy.Width - sourceX, _imageAdapter.Width - destX);
                int copyHeight = Math.Min(copy.Height - sourceY, _imageAdapter.Height - destY);

                return new MagitekResult.Failed("Pixel paste not yet supported in unified editor");
            }
            else if (copy is IndexedPixelCopy indexedCopy)
            {
                int copyWidth = Math.Min(copy.Width - sourceX, _imageAdapter.Width - destX);
                int copyHeight = Math.Min(copy.Height - sourceY, _imageAdapter.Height - destY);

                return new MagitekResult.Failed("Pixel paste not yet supported in unified editor");
            }
        }

        return new MagitekResult.Failed($"Unknown copy type: {paste.Copy.GetType()}");
    }

    [RelayCommand]
    public void DeleteElementSelection()
    {
        if (Selection.HasSelection)
        {
            DeleteElementSelectionInternal(Selection.SelectionRect);
            AddHistoryAction(new DeleteElementSelectionHistoryAction(Selection.SelectionRect));

            IsModified = true;
            Render();
        }
    }

    private void DeleteElementSelectionInternal(SnappedRectangle rect)
    {
        int startX = rect.SnappedLeft / WorkingArranger.ElementPixelSize.Width;
        int startY = rect.SnappedTop / WorkingArranger.ElementPixelSize.Height;
        int width = rect.SnappedWidth / WorkingArranger.ElementPixelSize.Height;
        int height = rect.SnappedHeight / WorkingArranger.ElementPixelSize.Width;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                WorkingArranger.ResetElement(x + startX, y + startY);
            }
        }
    }

    [RelayCommand]
    public async Task ResizeArranger()
    {
        var model = new ResizeTiledScatteredArrangerViewModel(_interactions, WorkingArranger.ArrangerElementSize.Width, WorkingArranger.ArrangerElementSize.Height);

        var dialogResult = await _interactions.RequestAsync(model);

        if (dialogResult is not null)
        {
            WorkingArranger.Resize(dialogResult.Width, dialogResult.Height);
            CreateImages();
            AddHistoryAction(new ResizeArrangerHistoryAction(dialogResult.Width, dialogResult.Height));

            IsModified = true;
        }
    }

    public bool CanRemapColors
    {
        get
        {
            if (!IsIndexedColor)
                return false;

            var palettes = WorkingArranger.GetReferencedPalettes();
            if (palettes?.Count <= 1)
                return WorkingArranger.GetReferencedCodecs().All(x => x.ColorType == PixelColorType.Indexed);

            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemapColors))]
    public async Task RemapColors()
    {
        if (!IsIndexedColor)
            return;

        var palette = WorkingArranger.GetReferencedPalettes().FirstOrDefault() ?? _paletteStore.DefaultPalette;

        var maxArrangerColors = WorkingArranger.EnumerateElements().OfType<ArrangerElement>().Select(x => x.Codec?.ColorDepth ?? 0).Max();
        var colors = Math.Min(256, 1 << maxArrangerColors);

        var remapViewModel = new ColorRemapViewModel(palette, colors, _colorFactory);
        var dialogResult = await _interactions.RequestAsync(remapViewModel);

        if (dialogResult is not null)
        {
            var remap = dialogResult.FinalColors.Select(x => (byte)x.Index).ToList();
            _imageAdapter.RemapColors(remap);
            Render();

            var remapAction = new ColorRemapHistoryAction(dialogResult.InitialColors, dialogResult.FinalColors);
            UndoHistory.Add(remapAction);
            IsModified = true;
        }
    }
}
