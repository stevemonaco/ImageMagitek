using System.Collections.Generic;
using System.Drawing;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;

public partial class GraphicsEditorViewModel
{
    [ObservableProperty] private List<string> _codecNames = [];
    [ObservableProperty] private string _selectedCodecName;
    [ObservableProperty] private bool _canCodecResize;
    [ObservableProperty] private int? _tiledElementWidth;
    [ObservableProperty] private int? _tiledElementHeight;

    partial void OnSelectedCodecNameChanged(string value)
    {
        if (value is not null)
            ChangeCodec();
    }

    partial void OnTiledElementWidthChanged(int? value)
    {
        if (value is > 0 && CanCodecResize)
            ChangeCodecDimensions(value.Value, TiledElementHeight ?? value.Value);
    }

    partial void OnTiledElementHeightChanged(int? value)
    {
        if (value is > 0 && CanCodecResize)
            ChangeCodecDimensions(TiledElementWidth ?? value.Value, value.Value);
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