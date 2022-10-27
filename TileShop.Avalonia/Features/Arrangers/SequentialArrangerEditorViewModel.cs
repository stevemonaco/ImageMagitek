using System;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Services;
using TileShop.Shared.Models;
using Jot;
using System.Drawing;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.Models;
using TileShop.Shared.Dialogs;
using TileShop.AvaloniaUI.Imaging;
using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.EventModels;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class SequentialArrangerEditorViewModel : ArrangerEditorViewModel
{
    private readonly ICodecService _codecService;
    private readonly IElementLayoutService _layoutService;
    private readonly Tracker _tracker;
    private IndexedImage _indexedImage;
    private DirectImage _directImage;
    private TileLayout _activeLayout;

    [ObservableProperty] private ObservableCollection<string> _codecNames = new();

    private string _selectedCodecName;
    public string SelectedCodecName
    {
        get => _selectedCodecName;
        set
        {
            if (SetProperty(ref _selectedCodecName, value))
                ChangeCodec();
        }
    }

    [ObservableProperty] private ObservableCollection<PaletteModel> _palettes = new();

    private PaletteModel _selectedPalette;
    public PaletteModel SelectedPalette
    {
        get => _selectedPalette;
        set
        {
            if (SetProperty(ref _selectedPalette, value))
                ChangePalette(SelectedPalette);
        }
    }

    [ObservableProperty] private ObservableCollection<string> _tileLayoutNames;

    private string _selectedTileLayoutName;
    public string SelectedTileLayoutName
    {
        get => _selectedTileLayoutName;
        set
        {
            if (SetProperty(ref _selectedTileLayoutName, value))
                ChangeElementLayout(_layoutService.ElementLayouts[_selectedTileLayoutName]);
        }
    }

    private int _tiledElementWidth = 8;
    public int TiledElementWidth
    {
        get => _tiledElementWidth;
        set
        {
            var preferredWidth = ((SequentialArranger)WorkingArranger).ActiveCodec.GetPreferredWidth(value);
            if (SetProperty(ref _tiledElementWidth, preferredWidth))
                ChangeCodecDimensions(TiledElementWidth, TiledElementHeight);
        }
    }

    private int _tiledElementHeight = 8;
    public int TiledElementHeight
    {
        get => _tiledElementHeight;
        set
        {
            var preferredHeight = ((SequentialArranger)WorkingArranger).ActiveCodec.GetPreferredHeight(value);
            if (SetProperty(ref _tiledElementHeight, preferredHeight))
                ChangeCodecDimensions(TiledElementWidth, TiledElementHeight);
        }
    }

    private int _tiledArrangerWidth = 8;
    public int TiledArrangerWidth
    {
        get => _tiledArrangerWidth;
        set
        {
            if (SetProperty(ref _tiledArrangerWidth, value))
                ResizeArranger(TiledArrangerWidth, TiledArrangerHeight);
        }
    }

    private int _tiledArrangerHeight = 16;
    public int TiledArrangerHeight
    {
        get => _tiledArrangerHeight;
        set
        {
            if (SetProperty(ref _tiledArrangerHeight, value))
                ResizeArranger(TiledArrangerWidth, TiledArrangerHeight);
        }
    }

    private int _linearArrangerWidth = 256;
    public int LinearArrangerWidth
    {
        get => _linearArrangerWidth;
        set
        {
            var preferredWidth = ((SequentialArranger)WorkingArranger).ActiveCodec.GetPreferredWidth(value);
            SetProperty(ref _linearArrangerWidth, preferredWidth);
            ChangeCodecDimensions(LinearArrangerWidth, LinearArrangerHeight);
        }
    }

    private int _linearArrangerHeight = 256;
    public int LinearArrangerHeight
    {
        get => _linearArrangerHeight;
        set
        {
            var preferredHeight = ((SequentialArranger)WorkingArranger).ActiveCodec.GetPreferredHeight(value);
            SetProperty(ref _linearArrangerHeight, preferredHeight);
            ChangeCodecDimensions(LinearArrangerWidth, LinearArrangerHeight);
        }
    }

    [ObservableProperty] private bool _canResize;
    [ObservableProperty] private int _elementWidthIncrement = 1;
    [ObservableProperty] private int _elementHeightIncrement = 1;
    [ObservableProperty] private int _arrangerWidthIncrement = 1;
    [ObservableProperty] private int _arrangerHeightIncrement = 1;

    private long _fileOffset;
    public long FileOffset
    {
        get => _fileOffset;
        set
        {
            if (SetProperty(ref _fileOffset, value))
                Move(_fileOffset);
        }
    }

    [ObservableProperty] private long _maxFileDecodingOffset;
    [ObservableProperty] private int _arrangerPageSize;

    public SequentialArrangerEditorViewModel(SequentialArranger arranger, IWindowManager windowManager,
        Tracker tracker, ICodecService codecService, IPaletteService paletteService, IElementLayoutService layoutService) :
        base(windowManager, paletteService)
    {
        Resource = arranger;
        WorkingArranger = arranger;
        _codecService = codecService;
        _layoutService = layoutService;
        _tracker = tracker;
        DisplayName = Resource?.Name ?? "Unnamed Arranger";

        CreateImages();

        CodecNames = new(codecService.GetSupportedCodecNames().OrderBy(x => x));
        _selectedCodecName = arranger.ActiveCodec.Name;

        TileLayoutNames = new(_layoutService.ElementLayouts.Select(x => x.Key).OrderBy(x => x));
        _selectedTileLayoutName = _layoutService.DefaultElementLayout.Name;
        _activeLayout = arranger.TileLayout;

        if (arranger.Layout == ElementLayout.Tiled)
        {
            _tiledElementWidth = arranger.ElementPixelSize.Width;
            _tiledElementHeight = arranger.ElementPixelSize.Height;
            _tiledArrangerHeight = arranger.ArrangerElementSize.Height;
            _tiledArrangerWidth = arranger.ArrangerElementSize.Width;
            SnapMode = SnapMode.Element;
        }
        else if (arranger.Layout == ElementLayout.Single)
        {
            _linearArrangerHeight = arranger.ArrangerPixelSize.Height;
            _linearArrangerWidth = arranger.ArrangerPixelSize.Width;
            SnapMode = SnapMode.Pixel;
        }

        CanChangeSnapMode = true;
        CanResize = arranger.ActiveCodec.CanResize;
        ElementWidthIncrement = arranger.ActiveCodec.WidthResizeIncrement;
        ElementHeightIncrement = arranger.ActiveCodec.HeightResizeIncrement;

        Palettes = new(_paletteService.GlobalPalettes.Select(x => new PaletteModel(x)));
        SelectedPalette = Palettes.First();

        ArrangerPageSize = (int)((SequentialArranger)WorkingArranger).ArrangerBitSize / 8;
        MaxFileDecodingOffset = ((SequentialArranger)WorkingArranger).FileSize - ArrangerPageSize;
    }

    public override void SaveChanges()
    {
        IsModified = false;
    }

    public override void DiscardChanges()
    {
        IsModified = false;
    }

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

    [RelayCommand]
    public void ExpandWidth()
    {
        if (IsTiledLayout)
            TiledArrangerWidth += ArrangerWidthIncrement;
        else
            LinearArrangerWidth += ElementWidthIncrement;
    }

    [RelayCommand]
    public void ExpandHeight()
    {
        if (IsTiledLayout)
            TiledArrangerHeight += ArrangerHeightIncrement;
        else
            LinearArrangerHeight += ElementHeightIncrement;
    }

    [RelayCommand]
    public void ShrinkWidth()
    {
        if (IsTiledLayout)
            TiledArrangerWidth = Math.Clamp(TiledArrangerWidth - ArrangerWidthIncrement, ArrangerWidthIncrement, int.MaxValue);
        else
            LinearArrangerHeight = Math.Clamp(LinearArrangerHeight - ElementWidthIncrement, ElementWidthIncrement, int.MaxValue);
    }

    [RelayCommand]
    public void ShrinkHeight()
    {
        if (IsTiledLayout)
            TiledArrangerHeight = Math.Clamp(TiledArrangerHeight - ArrangerHeightIncrement, ArrangerHeightIncrement, int.MaxValue);
        else
            LinearArrangerWidth = Math.Clamp(LinearArrangerWidth - ElementHeightIncrement, ElementHeightIncrement, int.MaxValue);
    }

    [RelayCommand]
    public async void JumpToOffset()
    {
        var model = new JumpToOffsetViewModel();
        _tracker.Track(model);

        var dialogResult = await _windowManager.ShowDialog(model);

        if (dialogResult is long result)
        {
            Move(result);
            _tracker.Persist(model);
        }
    }

    [RelayCommand]
    public void NewScatteredArrangerFromSelection()
    {
        if (SnapMode == SnapMode.Element)
        {
            int x = Selection.SelectionRect.SnappedLeft / WorkingArranger.ElementPixelSize.Width;
            int y = Selection.SelectionRect.SnappedTop / WorkingArranger.ElementPixelSize.Height;
            int width = Selection.SelectionRect.SnappedWidth / WorkingArranger.ElementPixelSize.Width;
            int height = Selection.SelectionRect.SnappedHeight / WorkingArranger.ElementPixelSize.Height;

            var copy = new ElementCopy(WorkingArranger, x, y, width, height);
            var model = new AddScatteredArrangerFromCopyEvent(copy, OriginatingProjectResource);
            Messenger.Send(model);
        }
        else
        {
            _windowManager.ShowMessageBox("Selection must be performed in Element Snap mode to create a new Scattered Arranger", "Error");
        }
    }

    [RelayCommand]
    public void NewScatteredArrangerFromImage()
    {
        if (SnapMode == SnapMode.Element)
        {
            var copy = new ElementCopy(WorkingArranger, 0, 0, 1, 1);
            var model = new AddScatteredArrangerFromCopyEvent(copy, OriginatingProjectResource);
            Messenger.Send(model);
        }
        else
        {
            _windowManager.ShowMessageBox("Selection must be performed in Element Snap mode to create a new Scattered Arranger", "Error");
        }
    }

    [RelayCommand]
    public void ApplyDefaultElementLayout()
    {
        ChangeElementLayout(_layoutService.DefaultElementLayout);
    }

    [RelayCommand]
    public async void CreateCustomLayout()
    {
        var model = new CustomElementLayoutViewModel();
        _tracker.Track(model);

        var dialogResult = await _windowManager.ShowDialog(model);

        if (dialogResult is not null)
        {
            var order = new List<Point>();
            if (model.FlowDirection == ElementLayoutFlowDirection.RowLeftToRight)
            {
                for (int y = 0; y < model.Height; y++)
                    for (int x = 0; x < model.Width; x++)
                        order.Add(new Point(x, y));
            }
            else if (model.FlowDirection == ElementLayoutFlowDirection.ColumnTopToBottom)
            {
                for (int x = 0; x < model.Width; x++)
                    for (int y = 0; y < model.Height; y++)
                        order.Add(new Point(x, y));
            }

            var layout = new TileLayout("Custom", model.Width, model.Height, model.Width * model.Height, order);
            ChangeElementLayout(layout);

            _tracker.Persist(model);
        }
    }

    [RelayCommand]
    public void ChangeElementLayout(string layoutName)
    {
        ChangeElementLayout(_layoutService.ElementLayouts[layoutName]);
    }

    private void Move(ArrangerMoveType moveType)
    {
        var oldAddress = ((SequentialArranger)WorkingArranger).Address;
        var newAddress = ((SequentialArranger)WorkingArranger).Move(moveType);

        if (oldAddress != newAddress)
        {
            _fileOffset = newAddress.ByteOffset;
            OnPropertyChanged(nameof(FileOffset));
            Render();
        }
    }

    private void Move(long offset)
    {
        var oldAddress = ((SequentialArranger)WorkingArranger).Address;
        var newAddress = ((SequentialArranger)WorkingArranger).Move(new BitAddress(offset, 0));

        if (oldAddress != newAddress)
        {
            _fileOffset = newAddress.ByteOffset;
            OnPropertyChanged(nameof(FileOffset));
            Render();
        }
    }

    private void ResizeArranger(int arrangerWidth, int arrangerHeight)
    {
        if (arrangerWidth <= 0 || arrangerHeight <= 0)
            return;

        if (arrangerWidth == WorkingArranger.ArrangerElementSize.Width &&
            arrangerHeight == WorkingArranger.ArrangerElementSize.Height && IsTiledLayout)
            return;

        if (arrangerWidth == WorkingArranger.ArrangerPixelSize.Width &&
            arrangerHeight == WorkingArranger.ArrangerPixelSize.Height && IsSingleLayout)
            return;

        ((SequentialArranger)WorkingArranger).Resize(arrangerWidth, arrangerHeight);
        CreateImages();
        ArrangerPageSize = (int)((SequentialArranger)WorkingArranger).ArrangerBitSize / 8;
        MaxFileDecodingOffset = ((SequentialArranger)WorkingArranger).FileSize - ArrangerPageSize;
    }

    [RelayCommand]
    public void SelectNextCodec()
    {
        var index = CodecNames.IndexOf(SelectedCodecName) + 1;
        SelectedCodecName = CodecNames[index % CodecNames.Count];
    }

    [RelayCommand]
    public void SelectPreviousCodec()
    {
        var index = CodecNames.IndexOf(SelectedCodecName) - 1;
        if (index < 0)
            index = CodecNames.Count - 1;
        SelectedCodecName = CodecNames[index];
    }

    [RelayCommand]
    public void ToggleSnapMode()
    {
        if (SnapMode == SnapMode.Element)
            SnapMode = SnapMode.Pixel;
        else if (SnapMode == SnapMode.Pixel)
            SnapMode = SnapMode.Element;
    }

    private void ChangeElementLayout(TileLayout layout)
    {
        ((SequentialArranger)WorkingArranger).ChangeElementLayout(layout);
        ArrangerWidthIncrement = layout.Width;
        ArrangerHeightIncrement = layout.Height;
        _tiledArrangerWidth = WorkingArranger.ArrangerElementSize.Width;
        _tiledArrangerHeight = WorkingArranger.ArrangerElementSize.Height;

        OnPropertyChanged(nameof(TiledArrangerWidth));
        OnPropertyChanged(nameof(TiledArrangerHeight));

        CreateImages();
    }

    private void ChangeCodec()
    {
        var codec = _codecService.CodecFactory.GetCodec(SelectedCodecName, default);
        if (codec.Layout == ImageMagitek.Codec.ImageLayout.Tiled)
        {
            _tiledElementHeight = codec.Height;
            _tiledElementWidth = codec.Width;

            ((SequentialArranger)WorkingArranger).ChangeCodec(codec, TiledArrangerWidth, TiledArrangerHeight);
            SnapMode = SnapMode.Element;

            OnPropertyChanged(nameof(TiledElementWidth));
            OnPropertyChanged(nameof(TiledElementHeight));
        }
        else if (codec.Layout == ImageMagitek.Codec.ImageLayout.Single)
        {
            ShowGridlines = false;
            _linearArrangerHeight = codec.Height;
            _linearArrangerWidth = codec.Width;

            ((SequentialArranger)WorkingArranger).ChangeCodec(codec, 1, 1);
            SnapMode = SnapMode.Pixel;

            OnPropertyChanged(nameof(LinearArrangerWidth));
            OnPropertyChanged(nameof(LinearArrangerHeight));
        }

        _fileOffset = ((SequentialArranger)WorkingArranger).Address.ByteOffset;
        ArrangerPageSize = (int)((SequentialArranger)WorkingArranger).ArrangerBitSize / 8;
        MaxFileDecodingOffset = ((SequentialArranger)WorkingArranger).FileSize - ArrangerPageSize;
        CanResize = codec.CanResize;
        ElementWidthIncrement = codec.WidthResizeIncrement;
        ElementHeightIncrement = codec.HeightResizeIncrement;
        CreateImages();

        OnPropertyChanged(nameof(FileOffset));
        OnPropertyChanged(nameof(IsTiledLayout));
        OnPropertyChanged(nameof(IsSingleLayout));
    }

    private void ChangePalette(PaletteModel pal)
    {
        ((SequentialArranger)WorkingArranger).ChangePalette(pal.Palette);
        Render();
    }

    private void ChangeCodecDimensions(int width, int height)
    {
        var codec = _codecService.CodecFactory.GetCodec(SelectedCodecName, new Size(width, height));
        ((SequentialArranger)WorkingArranger).ChangeCodec(codec);
        CreateImages();
    }

    private void CreateImages()
    {
        CancelOverlay();

        if (WorkingArranger.ColorType == PixelColorType.Indexed)
        {
            _indexedImage = new IndexedImage(WorkingArranger);
            BitmapAdapter = new IndexedBitmapAdapter(_indexedImage);
        }
        else if (WorkingArranger.ColorType == PixelColorType.Direct)
        {
            _directImage = new DirectImage(WorkingArranger);
            BitmapAdapter = new DirectBitmapAdapter(_directImage);
        }

        CreateGridlines();
    }

    protected override void CreateGridlines()
    {
        if (WorkingArranger.Layout == ElementLayout.Single)
        {
            CreateGridlines(0, 0, WorkingArranger.ArrangerPixelSize.Width, WorkingArranger.ArrangerPixelSize.Height, 8, 8);
        }
        else if (WorkingArranger.Layout == ElementLayout.Tiled)
        {
            base.CreateGridlines();
        }
    }

    public override void Render()
    {
        CancelOverlay();

        if (WorkingArranger.ColorType == PixelColorType.Indexed)
        {
            _indexedImage.Render();
            BitmapAdapter.Invalidate();
            OnImageModified?.Invoke();
        }
        else if (WorkingArranger.ColorType == PixelColorType.Direct)
        {
            _directImage.Render();
            BitmapAdapter.Invalidate();
            OnImageModified?.Invoke();
        }
    }

    #region Unsupported Operations due to SequentialArrangerEditor being read-only
    public override void Undo()
    {
        throw new NotSupportedException("Sequential Arrangers are read-only");
    }

    public override void Redo()
    {
        throw new NotSupportedException("Sequential Arrangers are read-only");
    }

    public override void ApplyHistoryAction(HistoryAction action)
    {
        throw new NotSupportedException("Sequential Arrangers are read-only");
    }

    public override void ApplyPaste(ArrangerPaste paste)
    {
        throw new NotSupportedException("Sequential Arrangers are read-only");
    }
    #endregion
}
