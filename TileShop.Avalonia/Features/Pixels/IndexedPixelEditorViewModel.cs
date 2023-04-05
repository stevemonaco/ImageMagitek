using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.Image;
using ImageMagitek.Services.Stores;
using Jot;
using TileShop.UI.Imaging;
using TileShop.UI.Models;
using TileShop.Shared.Interactions;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;
using Point = System.Drawing.Point;

namespace TileShop.UI.ViewModels;

public sealed partial class IndexedPixelEditorViewModel : PixelEditorViewModel<byte>
{
    private IndexedImage _indexedImage;

    [ObservableProperty] private ObservableCollection<PaletteModel> _palettes;
    [ObservableProperty] private PaletteModel _activePalette;

    public IndexedPixelEditorViewModel(Arranger arranger, Arranger projectArranger,
        IInteractionService interactionService, IColorFactory colorFactory, PaletteStore paletteStore, Tracker tracker)
        : base(projectArranger, interactionService, colorFactory, paletteStore, tracker)
    {
        Initialize(arranger, 0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height);
    }

    public IndexedPixelEditorViewModel(Arranger arranger, Arranger projectArranger, int viewX, int viewY, int viewWidth, int viewHeight,
        IInteractionService interactionService, IColorFactory colorFactory, PaletteStore paletteStore, Tracker tracker)
        : base(projectArranger, interactionService, colorFactory, paletteStore, tracker)
    {
        Initialize(arranger, viewX, viewY, viewWidth, viewHeight);
    }

    [MemberNotNull(nameof(_palettes), nameof(_activePalette), nameof(_indexedImage))]
    private void Initialize(Arranger arranger, int viewDx, int viewDy, int viewWidth, int viewHeight)
    {
        Resource = arranger;
        WorkingArranger = arranger.CloneArranger();
        ViewDx = viewDx;
        ViewDy = viewDy;
        _viewWidth = viewWidth;
        _viewHeight = viewHeight;

        var maxColors = WorkingArranger.EnumerateElementsWithinPixelRange(ViewDx, ViewDy, viewWidth, viewHeight)
            .OfType<ArrangerElement>()
            .Select(x => 1 << x.Codec.ColorDepth)
            .Max();

        var arrangerPalettes = WorkingArranger.EnumerateElementsWithinPixelRange(ViewDx, ViewDy, viewWidth, viewHeight)
            .OfType<ArrangerElement>()
            .Select(x => x.Codec)
            .OfType<IIndexedCodec>()
            .Select(x => x.Palette)
            .OfType<Palette>()
            .Distinct()
            .OrderBy(x => x.Name)
            .Select(x => new PaletteModel(x, Math.Min(maxColors, x.Entries)));

        _palettes = new(arrangerPalettes);
        OnPropertyChanged(nameof(Palettes));

        _indexedImage = new IndexedImage(WorkingArranger, ViewDx, ViewDy, _viewWidth, _viewHeight);
        BitmapAdapter = new IndexedBitmapAdapter(_indexedImage);

        DisplayName = $"Pixel Editor - {WorkingArranger.Name}";
        Selection = new ArrangerSelection(arranger, SnapMode);

        _gridSettings = GridSettingsViewModel.CreateDefault(_indexedImage);
        //_gridSettings = GridSettingsViewModel.CreateDefault(arranger);

        _activePalette = Palettes.First();
        OnPropertyChanged(nameof(ActivePalette));

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

    //protected override void CreateGridlines()
    //{
    //    if (WorkingArranger is null)
    //        return;

    //    if (WorkingArranger.Layout == ElementLayout.Single)
    //    {
    //        CreateGridlines(0, 0, _viewWidth, _viewHeight, 8, 8);
    //    }
    //    else if (WorkingArranger.Layout == ElementLayout.Tiled)
    //    {
    //        var location = WorkingArranger.PointToElementLocation(new Point(ViewDx, ViewDy));

    //        int x = WorkingArranger.ElementPixelSize.Width - (ViewDx - location.X * WorkingArranger.ElementPixelSize.Width);
    //        int y = WorkingArranger.ElementPixelSize.Height - (ViewDy - location.Y * WorkingArranger.ElementPixelSize.Height);

    //        CreateGridlines(x, y, _viewWidth, _viewHeight,
    //            WorkingArranger.ElementPixelSize.Width, WorkingArranger.ElementPixelSize.Height);
    //    }
    //}

    #region Commands
    [RelayCommand]
    public override void ApplyPaste(ArrangerPaste paste)
    {
        var message = ApplyPasteInternal(paste).Match(
            success =>
            {
                AddHistoryAction(new PasteArrangerHistoryAction(paste));

                IsModified = true;
                CancelOverlay();
                BitmapAdapter.Invalidate();

                return new NotifyStatusMessage("Paste successfully applied");
            },
            fail => new NotifyStatusMessage(fail.Reason)
            );

        Messenger.Send(message);
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
    public async Task RemapColors()
    {
        var palette = WorkingArranger.GetReferencedPalettes().FirstOrDefault() ?? _paletteStore.DefaultPalette;

        var maxArrangerColors = WorkingArranger.EnumerateElements().OfType<ArrangerElement>().Select(x => x.Codec?.ColorDepth ?? 0).Max();
        var colors = Math.Min(256, 1 << maxArrangerColors);

        var remapViewModel = new ColorRemapViewModel(palette, colors, _colorFactory);
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

        if (el is ArrangerElement { Codec: IIndexedCodec codec } element)
        {
            ActivePalette = Palettes.First(x => ReferenceEquals(x.Palette, codec.Palette));
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
            var changeMessage = new ArrangerChangedMessage(_projectArranger, ArrangerChange.Pixels);
            Messenger.Send(changeMessage);
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

        var message = result.Match(
            success =>
            {
                if (_activePencilHistory is not null && _activePencilHistory.ModifiedPoints.Add(new Point(x, y)))
                {
                    IsModified = true;
                    BitmapAdapter.Invalidate(x, y, 1, 1);
                    OnImageModified?.Invoke();
                }
                return new NotifyStatusMessage("");
            },
            fail => new NotifyStatusMessage(fail.Reason)
            );
        Messenger.Send(message);
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
