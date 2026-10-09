using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using Monaco.PathTree;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

public partial class GraphicsEditorViewModel
{
    [ObservableProperty] private bool _isPencilDrawing;
    [ObservableProperty] private DrawTool _selectedDrawTool = DrawTool.Pencil;
    [ObservableProperty] private ArrangeTool _selectedArrangeTool = ArrangeTool.ElementSelect;
    [ObservableProperty] private ViewTool _selectedViewTool = ViewTool.ElementSelect;

    public DrawTool DisplayedDrawTool => _modifierOverrideTool is not null && EditMode == GraphicsEditMode.Draw
        ? DrawTool.ColorPicker : SelectedDrawTool;

    public ArrangeTool DisplayedArrangeTool => _modifierOverrideTool is not null && EditMode == GraphicsEditMode.Arrange
        ? ArrangeTool.PickPalette : SelectedArrangeTool;
    partial void OnSelectedViewToolChanged(ViewTool value)
    {
        if (value == ViewTool.ElementSelect)
            SnapMode = SnapMode.Element;

        OnPropertyChanged(nameof(CanChangeSnapMode));
    }

    [ObservableProperty] private bool _areSymmetryToolsEnabled;

    partial void OnSelectedDrawToolChanged(DrawTool oldValue, DrawTool newValue)
    {
        OnPropertyChanged(nameof(DisplayedDrawTool));

        if (_pixelTools.TryGetValue(oldValue, out var outgoing))
        {
            var historyAction = outgoing.Deactivate(this);
            if (historyAction is not null)
                AddHistoryAction(historyAction);
        }
    }
    
    [RelayCommand]
    public void ChangeViewTool(ViewTool tool)
    {
        SelectedViewTool = tool;
    }

    [RelayCommand]
    public void ChangeArrangerTool(ArrangeTool tool)
    {
        SelectedArrangeTool = tool;
    }

    [RelayCommand]
    public void ToggleSymmetryTools()
    {
        AreSymmetryToolsEnabled = !AreSymmetryToolsEnabled;
        _preferencesStore.Preferences.EnableArrangerSymmetryTools = AreSymmetryToolsEnabled;
        _preferencesStore.Save();
    }

    public void SetSelectToolMode() => SelectedArrangeTool = ArrangeTool.ElementSelect;
    public void SetApplyPaletteMode() => SelectedArrangeTool = ArrangeTool.ApplyPalette;

    [RelayCommand]
    public void ToggleGridlineVisibility()
    {
        GridSettings.ShowGridlines ^= true;
        InvalidateEditor(InvalidationLevel.Overlay);
    }

    [RelayCommand]
    public async Task ModifyGridSettings()
    {
        var original = GridSettings.Capture();
        var model = new ModifyGridSettingsViewModel(original, GridSettingsViewModel.DefaultSpacing(WorkingArranger))
        {
            SettingsChanged = snapshot => GridSettings.Apply(snapshot, WorkingArranger)
        };

        var result = await _interactions.RequestAsync(model);

        if (result is null)
        {
            GridSettings.Apply(original, WorkingArranger);
            return;
        }

        GridSettings.Apply(result, WorkingArranger);
        _preferencesStore.Preferences.Grid = GridSettings.ToPreferences();
        _preferencesStore.Save();
    }

    internal bool TryApplyPalette(int pixelX, int pixelY, Palette palette)
    {
        if (!IsIndexedColor)
            return false;

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
            InvalidateEditor(InvalidationLevel.Display);

        return needsRender;
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
                    IsModified = true;
                    return true;
                },
                fail => false);
        }
        return false;
    }

    internal bool CanApplyPaletteAtPosition(int pixelX, int pixelY) =>
        SelectedPalette is not null && _imageAdapter.CanSetPalette(pixelX, pixelY, SelectedPalette.Palette).HasSucceeded;

    internal bool CanPickPaletteAtPosition(int pixelX, int pixelY) =>
        IsIndexedColor && WorkingArranger.GetElementAtPixel(pixelX, pixelY)?.Codec is IIndexedCodec;

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

        var projectTree = _projectService.FindContainingProject(Resource);
        var projectPalettes = projectTree?.EnumerateDepthFirst()
            .Where(x => x.Item is Palette)
            .Select(x => new AssociatePaletteModel((Palette)x.Item, projectTree.CreatePathKey(x)));
        var palettes = (projectPalettes ?? [])
            .Concat(_paletteStore.GlobalPalettes.Select(x => new AssociatePaletteModel(x, x.Name)));

        var model = new AssociatePaletteViewModel(palettes);
        var dialogResult = await _interactions.RequestAsync(model);

        if (dialogResult is not null)
        {
            if (dialogResult.SelectedPalette.Palette.DataSource is FileDataSource { IsMissing: true } missing)
            {
                await _interactions.AlertAsync("Associate Palette",
                    $"'{dialogResult.SelectedPalette.Palette.Name}' is unavailable because data file '{missing.Name}' is missing. Right-click it in the project tree and choose Relink... to repair it.");
                return;
            }

            var palModel = new PaletteModel(dialogResult.SelectedPalette.Palette, dialogResult.SelectedPalette.Palette.Entries);
            Palettes.Add(palModel);
        }
    }

    [RelayCommand]
    public void ApplyPaste(ArrangerPaste paste)
    {
        var elementCopy = IsArrangerMode && IsTiledLayout && WorkingArranger is ScatteredArranger ? paste.Copy as ElementCopy : null;
        var result = elementCopy is not null ? ApplyElementPaste(paste, elementCopy) : ApplyPixelPaste(paste);

        var message = result.Match(
            success =>
            {
                HistoryAction action = elementCopy is not null
                    ? new ElementPasteHistoryAction()
                    : new PasteArrangerHistoryAction(paste.Copy, paste.Rect.SnappedLeft, paste.Rect.SnappedTop, DrawClipBounds);
                AddHistoryAction(action);
                IsModified = true;
                CancelOverlay();

                InvalidateEditor(elementCopy is not null ? InvalidationLevel.PixelData : InvalidationLevel.Display);

                return new NotifyStatusMessage("Paste successfully applied");
            },
            fail => new NotifyStatusMessage(fail.Reason)
        );

        Messenger.Send(message);
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
        if (!CanAcceptPixelPastes)
            return new MagitekResult.Failed("Arranger is read-only");

        return GraphicsEditHistory.ApplyPixelPaste(_imageAdapter, paste.Copy, paste.Rect.SnappedLeft, paste.Rect.SnappedTop, DrawClipBounds);
    }

    public bool CanDeleteElementSelection => IsArrangerMode && WorkingArranger is ScatteredArranger
        && Selection.HasSelection && Selection.SelectionRect.SnapMode == SnapMode.Element;

    [RelayCommand(CanExecute = nameof(CanDeleteElementSelection))]
    public void DeleteElementSelection()
    {
        if (!CanDeleteElementSelection)
            return;

        ResetElements(WorkingArranger, Selection.SelectionRect);
        AddHistoryAction(new DeleteElementSelectionHistoryAction(Selection.SelectionRect));

        IsModified = true;
        InvalidateEditor(InvalidationLevel.PixelData);
    }

    /// <summary>
    /// Resets every element of <paramref name="arranger"/> covered by the element-snapped <paramref name="rect"/>
    /// </summary>
    public static void ResetElements(Arranger arranger, SnappedRectangle rect)
    {
        int startX = rect.SnappedLeft / arranger.ElementPixelSize.Width;
        int startY = rect.SnappedTop / arranger.ElementPixelSize.Height;
        int width = rect.SnappedWidth / arranger.ElementPixelSize.Width;
        int height = rect.SnappedHeight / arranger.ElementPixelSize.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                arranger.ResetElement(x + startX, y + startY);
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

    #region Sequential Arranger Move Commands
    [RelayCommand] public void MoveByteDown() => Move(ArrangerMoveType.ByteDown);
    [RelayCommand] public void MoveByteUp() => Move(ArrangerMoveType.ByteUp);
    [RelayCommand] public void MoveRowDown() => Move(ArrangerMoveType.RowDown);
    [RelayCommand] public void MoveRowUp() => Move(ArrangerMoveType.RowUp);
    [RelayCommand] public void MoveColumnRight() => Move(ArrangerMoveType.ColRight);
    [RelayCommand] public void MoveColumnLeft() => Move(ArrangerMoveType.ColLeft);
    [RelayCommand] public void MovePageDown() => Move(ArrangerMoveType.PageDown);
    [RelayCommand] public void MovePageUp() => Move(ArrangerMoveType.PageUp);
    [RelayCommand] public void MoveHome() => Move(ArrangerMoveType.Home);
    [RelayCommand] public void MoveEnd() => Move(ArrangerMoveType.End);

    [RelayCommand(CanExecute = nameof(IsSequentialArranger))]
    public async Task JumpToOffset()
    {
        var model = new JumpToOffsetViewModel(FileOffset, _preferencesStore.Preferences.JumpToOffsetBase);
        var result = await _interactions.RequestAsync(model);

        if (result is not long offset)
            return;

        MoveToOffset(offset);
        _preferencesStore.Preferences.JumpToOffsetBase = model.NumericBase;
        _preferencesStore.Save();
    }

    private void Move(ArrangerMoveType moveType)
    {
        if (WorkingArranger is not SequentialArranger seqArr)
            return;

        var oldAddress = seqArr.Address;
        var newAddress = seqArr.Move(moveType);

        if (oldAddress != newAddress)
        {
            _fileOffset = newAddress.ByteOffset;
            OnPropertyChanged(nameof(FileOffset));
            InvalidateEditor(InvalidationLevel.PixelData);
        }
    }

    private void MoveToOffset(long offset)
    {
        if (WorkingArranger is not SequentialArranger seqArr)
            return;

        var oldAddress = seqArr.Address;
        var newAddress = seqArr.Move(new BitAddress(offset, 0));

        if (oldAddress != newAddress)
        {
            _fileOffset = newAddress.ByteOffset;
            OnPropertyChanged(nameof(FileOffset));
            InvalidateEditor(InvalidationLevel.PixelData);
        }
    }
    #endregion

    #region Sequential Arranger Expand/Shrink Commands
    public bool CanResizeSequentialArranger => IsSequentialArranger && IsViewMode;

    private void NotifyResizeCommandsChanged()
    {
        ExpandWidthCommand.NotifyCanExecuteChanged();
        ExpandHeightCommand.NotifyCanExecuteChanged();
        ShrinkWidthCommand.NotifyCanExecuteChanged();
        ShrinkHeightCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanResizeSequentialArranger))]
    public void ExpandWidth()
    {
        if (WorkingArranger is not SequentialArranger)
            return;

        if (IsTiledLayout)
            TiledArrangerWidth += ArrangerWidthIncrement;
        else
            LinearArrangerWidth += ElementWidthIncrement;
    }

    [RelayCommand(CanExecute = nameof(CanResizeSequentialArranger))]
    public void ExpandHeight()
    {
        if (WorkingArranger is not SequentialArranger)
            return;

        if (IsTiledLayout)
            TiledArrangerHeight += ArrangerHeightIncrement;
        else
            LinearArrangerHeight += ElementHeightIncrement;
    }

    [RelayCommand(CanExecute = nameof(CanResizeSequentialArranger))]
    public void ShrinkWidth()
    {
        if (WorkingArranger is not SequentialArranger)
            return;

        if (IsTiledLayout)
            TiledArrangerWidth = Math.Clamp(TiledArrangerWidth - ArrangerWidthIncrement, ArrangerWidthIncrement, int.MaxValue);
        else
            LinearArrangerWidth = Math.Clamp(LinearArrangerWidth - ElementWidthIncrement, ElementWidthIncrement, int.MaxValue);
    }

    [RelayCommand(CanExecute = nameof(CanResizeSequentialArranger))]
    public void ShrinkHeight()
    {
        if (WorkingArranger is not SequentialArranger)
            return;

        if (IsTiledLayout)
            TiledArrangerHeight = Math.Clamp(TiledArrangerHeight - ArrangerHeightIncrement, ArrangerHeightIncrement, int.MaxValue);
        else
            LinearArrangerHeight = Math.Clamp(LinearArrangerHeight - ElementHeightIncrement, ElementHeightIncrement, int.MaxValue);
    }

    private void ResizeSequentialArranger(int arrangerWidth, int arrangerHeight)
    {
        if (WorkingArranger is not SequentialArranger seqArr)
            return;

        if (arrangerWidth <= 0 || arrangerHeight <= 0)
            return;

        if (IsTiledLayout)
        {
            var layout = seqArr.TileLayout;
            arrangerWidth = Math.Max(layout.Width, arrangerWidth - arrangerWidth % layout.Width);
            arrangerHeight = Math.Max(layout.Height, arrangerHeight - arrangerHeight % layout.Height);
            _tiledArrangerWidth = arrangerWidth;
            _tiledArrangerHeight = arrangerHeight;
            OnPropertyChanged(nameof(TiledArrangerWidth));
            OnPropertyChanged(nameof(TiledArrangerHeight));
        }

        if (arrangerWidth == WorkingArranger.ArrangerElementSize.Width &&
            arrangerHeight == WorkingArranger.ArrangerElementSize.Height && IsTiledLayout)
            return;

        if (arrangerWidth == WorkingArranger.ArrangerPixelSize.Width &&
            arrangerHeight == WorkingArranger.ArrangerPixelSize.Height && IsSingleLayout)
            return;

        if (IsTiledLayout)
        {
            seqArr.Resize(arrangerWidth, arrangerHeight);
        }

        CreateImages();
        ArrangerPageSize = (int)seqArr.ArrangerBitSize / 8;
        MaxFileDecodingOffset = seqArr.FileSize - ArrangerPageSize;
    }
    #endregion

    public bool CanRemapColors
    {
        get
        {
            if (!IsIndexedColor || WorkingArranger.IsReadOnly())
                return false;

            var bounds = GetRemapBounds();
            if (bounds is { IsEmpty: true })
                return false;

            var elements = GetRemapElements(bounds).ToList();
            return elements.Count > 0
                && elements.All(x => x.Codec.ColorType == PixelColorType.Indexed)
                && GetPalettes(elements).Count() <= 1;
        }
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(CanRemapColors));
        RemapColorsCommand.NotifyCanExecuteChanged();
        CopySelectionCommand.NotifyCanExecuteChanged();
        CutSelectionCommand.NotifyCanExecuteChanged();
        DeleteElementSelectionCommand.NotifyCanExecuteChanged();
        ImportIntoSelectionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemapColors))]
    public async Task RemapColors()
    {
        if (!CanRemapColors)
            return;

        var bounds = GetRemapBounds();
        var elements = GetRemapElements(bounds).ToList();
        var palette = GetPalettes(elements).FirstOrDefault() ?? _paletteStore.DefaultPalette;

        var maxArrangerColors = elements.Max(x => x.Codec.ColorDepth);
        var colors = Math.Min(256, 1 << maxArrangerColors);

        var remapViewModel = new ColorRemapViewModel(palette, colors, _colorFactory)
        {
            ScopeDescription = Selection.HasSelection ? "Applies to the current selection" :
                bounds is not null ? "Applies within the draw clip" : "Applies to the entire image"
        };
        var dialogResult = await _interactions.RequestAsync(remapViewModel);

        if (dialogResult is { HasChanges: true })
        {
            var remap = dialogResult.CreateRemap();
            _imageAdapter.RemapColors(remap, bounds);
            InvalidateEditor(InvalidationLevel.Display);

            AddHistoryAction(new ColorRemapHistoryAction(remap, bounds));
            IsModified = true;
        }
    }

    /// <summary>
    /// Region a color remap applies to: the selection limited by the draw clip, or null for the entire image
    /// </summary>
    private Rectangle? GetRemapBounds()
    {
        var clipBounds = DrawClipBounds;

        if (!Selection.HasSelection)
            return clipBounds;

        var rect = Selection.SelectionRect;
        var selectionBounds = new Rectangle(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
        return clipBounds is { } clip ? Rectangle.Intersect(selectionBounds, clip) : selectionBounds;
    }

    private IEnumerable<ArrangerElement> GetRemapElements(Rectangle? bounds)
    {
        if (bounds is not { } b)
            return WorkingArranger.EnumerateElements().OfType<ArrangerElement>();

        return WorkingArranger.EnumerateElementLocationsWithinPixelRange(b.X, b.Y, b.Width, b.Height)
            .Select(loc => WorkingArranger.GetElement(loc.X, loc.Y))
            .OfType<ArrangerElement>();
    }

    private static IEnumerable<Palette> GetPalettes(IEnumerable<ArrangerElement> elements) =>
        elements.Select(x => x.Codec).OfType<IIndexedCodec>().Select(x => x.Palette).OfType<Palette>().Distinct();
}
