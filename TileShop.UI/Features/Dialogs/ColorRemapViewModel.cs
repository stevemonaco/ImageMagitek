using Avalonia.Media;
using ImageMagitek.Colors;
using System.Collections.ObjectModel;
using TileShop.UI.Models;
using TileShop.UI.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.UI.ViewModels;

public partial class ColorRemapViewModel : RequestBaseViewModel<ColorRemapViewModel> //, IDropTarget //, IDropTarget, IDragSource
{
    private readonly IColorFactory _colorFactory;

    [ObservableProperty] private ObservableCollection<RemappableColorModel> _initialColors = new();
    [ObservableProperty] private ObservableCollection<RemappableColorModel> _finalColors = new();

    /// <summary>
    /// ViewModel responsible for remapping Palette colors of an indexed image
    /// </summary>
    /// <param name="palette">Palette containing the colors</param>
    public ColorRemapViewModel(Palette palette, IColorFactory colorFactory) : this(palette, palette.Entries, colorFactory)
    {
    }

    /// <summary>
    /// ViewModel responsible for remapping Palette colors of an indexed image
    /// </summary>
    /// <param name="palette">Palette containing the colors</param>
    /// <param name="paletteEntries">Number of colors to remap starting with the 0-index</param>
    /// <param name="colorFactory">Factory to create/convert colors</param>
    public ColorRemapViewModel(Palette palette, int paletteEntries, IColorFactory colorFactory)
    {
        _colorFactory = colorFactory;

        for (int i = 0; i < paletteEntries; i++)
        {
            var nativeColor = _colorFactory.ToNative(palette[i]);
            var color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
            InitialColors.Add(new RemappableColorModel(color, i));
            FinalColors.Add(new RemappableColorModel(color, i));
        }

        Title = "Color Remapper";
        AcceptName = "Remap";
    }

    public override ColorRemapViewModel? ProduceResult() => this;

    protected override void Accept()
    {
        RequestResult = this;
        OnPropertyChanged(nameof(RequestResult));
    }
}
