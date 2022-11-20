using System;
using System.Linq;
using System.Collections.ObjectModel;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using TileShop.Shared.EventModels;
using TileShop.AvaloniaUI.Imaging;
using TileShop.AvaloniaUI.Models;
using TileShop.Shared.Models;
using ImageMagitek.Image;
using ImageMagitek.ExtensionMethods;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;
using TileShop.Shared.Interactions;

using Point = System.Drawing.Point;

namespace TileShop.AvaloniaUI.ViewModels;

public sealed partial class IndexedPixelEditorViewModel : PixelEditorViewModel<byte>
{
    private IndexedImage _indexedImage = null!;

    [ObservableProperty] private ObservableCollection<PaletteModel> _palettes = null!;
    [ObservableProperty] private PaletteModel _activePalette = null!;

    public IndexedPixelEditorViewModel(Arranger arranger, Arranger projectArranger,
        IInteractionService interactionService, IPaletteService paletteService)
        : base(projectArranger, interactionService, paletteService)
    {
        Initialize(arranger, 0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height);
    }

    public IndexedPixelEditorViewModel(Arranger arranger, Arranger projectArranger, int viewX, int viewY, int viewWidth, int viewHeight,
        IInteractionService interactionService, IPaletteService paletteService)
        : base(projectArranger, interactionService, paletteService)
    {
        Initialize(arranger, viewX, viewY, viewWidth, viewHeight);
    }

    private void Initialize(Arranger arranger, int viewX, int viewY, int viewWidth, int viewHeight)
    {
        Resource = arranger;
        WorkingArranger = arranger.CloneArranger();
        _viewX = viewX;
        _viewY = viewY;
        _viewWidth = viewWidth;
        _viewHeight = viewHeight;

        var maxColors = WorkingArranger.EnumerateElementsWithinPixelRange(viewX, viewY, viewWidth, viewHeight)
            .OfType<ArrangerElement>()
            .Select(x => 1 << x.Codec.ColorDepth)
            .Max();

        var arrangerPalettes = WorkingArranger.EnumerateElementsWithinPixelRange(viewX, viewY, viewWidth, viewHeight)
            .OfType<ArrangerElement>()
            .Select(x => x.Palette)
            .Distinct()
            .OrderBy(x => x.Name)
            .Select(x => new PaletteModel(x, Math.Min(maxColors, x.Entries)));

        Palettes = new(arrangerPalettes);

        _indexedImage = new IndexedImage(WorkingArranger, _viewX, _viewY, _viewWidth, _viewHeight);
        BitmapAdapter = new IndexedBitmapAdapter(_indexedImage);

        DisplayName = $"Pixel Editor - {WorkingArranger.Name}";
        Selection = new ArrangerSelection(arranger, SnapMode);

        CreateGridlines();

        ActivePalette = Palettes.First();
        PrimaryColor = 0;
        SecondaryColor = 1;
        OnPropertyChanged(nameof(CanRemapColors));
    }

    public override void Render()
    {
        BitmapAdapter.Invalidate();
        OnImageModified?.Invoke();
    }

    protected override void ReloadImage() => _indexedImage.Render();

    protected override void CreateGridlines()
    {
        if (WorkingArranger is null)
            return;

        if (WorkingArranger.Layout == ElementLayout.Single)
        {
            CreateGridlines(0, 0, _viewWidth, _viewHeight, 8, 8);
        }
        else if (WorkingArranger.Layout == ElementLayout.Tiled)
        {
            var location = WorkingArranger.PointToElementLocation(new Point(_viewX, _viewY));

            int x = WorkingArranger.ElementPixelSize.Width - (_viewX - location.X * WorkingArranger.ElementPixelSize.Width);
            int y = WorkingArranger.ElementPixelSize.Height - (_viewY - location.Y * WorkingArranger.ElementPixelSize.Height);

            CreateGridlines(x, y, _viewWidth, _viewHeight,
                WorkingArranger.ElementPixelSize.Width, WorkingArranger.ElementPixelSize.Height);
        }
    }

    #region Commands
    [RelayCommand]
    public override void ApplyPaste(ArrangerPaste paste)
    {
        var notifyEvent = ApplyPasteInternal(paste).Match(
            success =>
            {
                AddHistoryAction(new PasteArrangerHistoryAction(paste));

                IsModified = true;
                CancelOverlay();
                BitmapAdapter.Invalidate();

                return new NotifyOperationEvent("Paste successfully applied");
            },
            fail => new NotifyOperationEvent(fail.Reason)
            );

        Messenger.Send(notifyEvent);
    }

    public bool CanRemapColors
    {
        get
        {
            var palettes = WorkingArranger.GetReferencedPalettes();
            if (palettes?.Count <= 1)
                return WorkingArranger.GetReferencedCodecs().All(x => x.ColorType == PixelColorType.Indexed);

            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemapColors))]
    public async void RemapColors()
    {
        var palette = WorkingArranger.GetReferencedPalettes().FirstOrDefault() ?? _paletteService.DefaultPalette;

        var maxArrangerColors = WorkingArranger.EnumerateElements().OfType<ArrangerElement>().Select(x => x.Codec?.ColorDepth ?? 0).Max();
        var colors = Math.Min(256, 1 << maxArrangerColors);

        var remapViewModel = new ColorRemapViewModel(palette, colors, _paletteService.ColorFactory);
        var dialogResult = await _interactions.RequestAsync(remapViewModel);

        if (dialogResult is not null)
        {
            var remap = dialogResult.FinalColors.Select(x => (byte)x.Index).ToList();
            _indexedImage.RemapColors(remap);
            Render();

            var remapAction = new ColorRemapHistoryAction(dialogResult.InitialColors, dialogResult.FinalColors);
            UndoHistory.Add(remapAction);
            IsModified = true;
        }
    }

    public override void PickColor(int x, int y, ColorPriority priority)
    {
        var el = WorkingArranger.GetElementAtPixel(x, y);

        if (el is ArrangerElement element)
        {
            ActivePalette = Palettes.First(x => ReferenceEquals(x.Palette, element.Palette));
            base.PickColor(x, y, priority);
        }
    }

    [RelayCommand]
    public override async Task SaveChangesAsync()
    {
        try
        {
            _indexedImage.SaveImage();

            UndoHistory.Clear();
            RedoHistory.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));

            IsModified = false;
            var changeEvent = new ArrangerChangedEvent(_projectArranger, ArrangerChange.Pixels);
            Messenger.Send(changeEvent);
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync("Save Error", $"Could not save the pixel arranger contents\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void DiscardChanges()
    {
        _indexedImage.Render();
        UndoHistory.Clear();
        RedoHistory.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public override void SetPixel(int x, int y, byte color)
    {
        var modelColor = ActivePalette.Colors[color].Color;
        var palColor = new ColorRgba32(modelColor.R, modelColor.G, modelColor.B, modelColor.A);
        var result = _indexedImage.TrySetPixel(x, y, palColor);

        var notifyEvent = result.Match(
            success =>
            {
                if (_activePencilHistory is not null && _activePencilHistory.ModifiedPoints.Add(new Point(x, y)))
                {
                    IsModified = true;
                    BitmapAdapter.Invalidate(x, y, 1, 1);
                    OnImageModified?.Invoke();
                }
                return new NotifyOperationEvent("");
            },
            fail => new NotifyOperationEvent(fail.Reason)
            );
        Messenger.Send(notifyEvent);
    }

    public override byte GetPixel(int x, int y) => _indexedImage.GetPixel(x, y);

    public override void FloodFill(int x, int y, byte fillColor)
    {
        if (_indexedImage.FloodFill(x, y, fillColor))
        {
            AddHistoryAction(new FloodFillAction<byte>(x, y, fillColor));
            IsModified = true;
            Render();
        }
    }

    #endregion

    private MagitekResult ApplyPasteInternal(ArrangerPaste paste)
    {
        int destX = Math.Max(0, paste.Rect.SnappedLeft);
        int destY = Math.Max(0, paste.Rect.SnappedTop);
        int sourceX = paste.Rect.SnappedLeft >= 0 ? 0 : -paste.Rect.SnappedLeft;
        int sourceY = paste.Rect.SnappedTop >= 0 ? 0 : -paste.Rect.SnappedTop;

        var destStart = new Point(destX, destY);
        var sourceStart = new Point(sourceX, sourceY);

        ArrangerCopy? copy = default;

        if (paste.Copy is ElementCopy elementCopy)
            copy = elementCopy.ToPixelCopy();
        else
            copy = paste.Copy;

        if (copy is IndexedPixelCopy indexedCopy)
        {
            int copyWidth = Math.Min(copy.Width - sourceX, _indexedImage.Width - destX);
            int copyHeight = Math.Min(copy.Height - sourceY, _indexedImage.Height - destY);

            return ImageCopier.CopyPixels(indexedCopy.Image, _indexedImage, sourceStart, destStart, copyWidth, copyHeight,
                PixelRemapOperation.RemapByExactPaletteColors, PixelRemapOperation.RemapByExactIndex);
        }
        else if (copy is DirectPixelCopy directCopy)
        {
            throw new NotImplementedException("Direct->Indexed pasting is not yet implemented");
            //var sourceImage = (Paste.OverlayImage as DirectBitmapAdapter).Image;
            //result = ImageCopier.CopyPixels(sourceImage, _indexedImage, sourceStart, destStart, copyWidth, copyHeight,
            //    ImageRemapOperation.RemapByExactPaletteColors, ImageRemapOperation.RemapByExactIndex);
        }
        else
            throw new InvalidOperationException($"{nameof(ApplyPaste)} attempted to copy from an arranger of type {paste.Copy.Source.ColorType} to {WorkingArranger.ColorType}");
    }

    public override void ApplyHistoryAction(HistoryAction action)
    {
        if (action is PencilHistoryAction<byte> pencilAction)
        {
            foreach (var point in pencilAction.ModifiedPoints)
                _indexedImage.SetPixel(point.X, point.Y, pencilAction.PencilColor);
        }
        else if (action is FloodFillAction<byte> floodFillAction)
        {
            _indexedImage.FloodFill(floodFillAction.X, floodFillAction.Y, floodFillAction.FillColor);
        }
        else if (action is ColorRemapHistoryAction remapAction)
        {
            _indexedImage.RemapColors(remapAction.FinalColors.Select(x => (byte)x.Index).ToList());
        }
        else if (action is PasteArrangerHistoryAction pasteAction)
        {
            ApplyPasteInternal(pasteAction.Paste);
        }
    }
}
