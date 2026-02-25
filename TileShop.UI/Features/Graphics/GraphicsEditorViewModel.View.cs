using System.Collections.Generic;
using System.Drawing;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;

/// <summary>
/// Contains View properties, primarily for Sequential Arrangers
/// </summary>
public partial class GraphicsEditorViewModel
{
    [ObservableProperty] private List<string> _codecNames = [];
    [ObservableProperty] private string _selectedCodecName;
    [ObservableProperty] private bool _canCodecResize;
    
    private long _fileOffset;
    public long FileOffset
    {
        get => _fileOffset;
        set
        {
            if (SetProperty(ref _fileOffset, value))
                MoveToOffset(_fileOffset);
        }
    }

    [ObservableProperty] private long _maxFileDecodingOffset;
    [ObservableProperty] private int _arrangerPageSize;

    private int _tiledArrangerWidth = 8;
    public int TiledArrangerWidth
    {
        get => _tiledArrangerWidth;
        set
        {
            if (SetProperty(ref _tiledArrangerWidth, value))
                ResizeSequentialArranger(TiledArrangerWidth, TiledArrangerHeight);
        }
    }

    private int _tiledArrangerHeight = 16;
    public int TiledArrangerHeight
    {
        get => _tiledArrangerHeight;
        set
        {
            if (SetProperty(ref _tiledArrangerHeight, value))
            {
                ResizeSequentialArranger(TiledArrangerWidth, TiledArrangerHeight);
            }
        }
    }

    private int _linearArrangerWidth = 256;
    public int LinearArrangerWidth
    {
        get => _linearArrangerWidth;
        set
        {
            if (WorkingArranger is SequentialArranger seqArr)
            {
                var preferredWidth = ((SequentialArranger)WorkingArranger).ActiveCodec.GetPreferredWidth(value);
                SetProperty(ref _linearArrangerWidth, preferredWidth);
                ChangeCodecDimensions(LinearArrangerWidth, LinearArrangerHeight);
            }
        }
    }

    private int _linearArrangerHeight = 256;
    public int LinearArrangerHeight
    {
        get => _linearArrangerHeight;
        set
        {
            if (WorkingArranger is SequentialArranger seqArr)
            {
                var preferredHeight = ((SequentialArranger)WorkingArranger).ActiveCodec.GetPreferredHeight(value);
                SetProperty(ref _linearArrangerHeight, preferredHeight);
                ChangeCodecDimensions(LinearArrangerWidth, LinearArrangerHeight);
            }
        }
    }

    [ObservableProperty] private int _arrangerWidthIncrement = 1;
    [ObservableProperty] private int _arrangerHeightIncrement = 1;
    [ObservableProperty] private int _elementWidthIncrement = 1;
    [ObservableProperty] private int _elementHeightIncrement = 1;

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

    partial void OnSelectedCodecNameChanged(string value)
    {
        if (value is not null)
            ChangeCodec();
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
        var codec = _codecService.CodecFactory.CreateCodec(SelectedCodecName, default);
        Guard.IsNotNull(codec);

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
            GridSettings.ShowGridlines = false;
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
        CanCodecResize = codec.CanResize;
        ElementWidthIncrement = codec.WidthResizeIncrement;
        ElementHeightIncrement = codec.HeightResizeIncrement;
        CreateImages();
        GridSettings.AdjustGridlines(WorkingArranger);

        OnPropertyChanged(nameof(FileOffset));
        OnPropertyChanged(nameof(IsTiledLayout));
        OnPropertyChanged(nameof(IsSingleLayout));
    }

    // private void ChangePalette(PaletteModel pal)
    // {
    //     ((SequentialArranger)WorkingArranger).ChangePalette(pal.Palette);
    //     Render();
    // }

    private bool ChangeCodecDimensions(int width, int height)
    {
        var codec = _codecService.CodecFactory.CreateCodec(SelectedCodecName, new Size(width, height));

        if (codec is null)
            return false;

        ((SequentialArranger)WorkingArranger).ChangeCodec(codec);
        CreateImages();
        return true;
    }
}