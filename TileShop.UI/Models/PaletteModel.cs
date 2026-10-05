using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Colors;
using TileShop.UI.Converters;

namespace TileShop.UI.Models;

public partial class PaletteModel : ObservableObject
{
    private readonly int _maxColors;

    [ObservableProperty] private string _name;
    [ObservableProperty] private ObservableCollection<PaletteEntry> _colors = new();

    public Palette Palette { get; }

    public PaletteModel(Palette pal) : this(pal, pal.Entries) { }

    public PaletteModel(Palette pal, int maxColors)
    {
        _name = pal.Name;
        _maxColors = maxColors;
        Palette = pal;

        AddColors();
    }

    /// <summary>
    /// Rebuilds the swatches after the palette's colors change
    /// </summary>
    public void Refresh()
    {
        Colors.Clear();
        AddColors();
    }

    private void AddColors()
    {
        int colorCount = Math.Min(Palette.Entries, _maxColors);

        for (int i = 0; i < colorCount; i++)
        {
            var color = ColorRgba32ToMediaColorConverter.ToMediaColor(Palette[i]);

            var entry = new PaletteEntry((byte)i, color);
            Colors.Add(entry);
        }
    }
}
