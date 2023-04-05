using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Colors;

namespace TileShop.UI.Models;

public partial class PaletteModel : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private ObservableCollection<PaletteEntry> _colors = new();

    public Palette Palette { get; }

    public PaletteModel(Palette pal) : this(pal, pal.Entries) { }

    public PaletteModel(Palette pal, int maxColors)
    {
        _name = pal.Name;
        Palette = pal;

        int colorCount = Math.Min(pal.Entries, maxColors);

        for (int i = 0; i < colorCount; i++)
        {
            var color = new Color(pal[i].A, pal[i].R, pal[i].G, pal[i].B);

            var entry = new PaletteEntry((byte)i, color);
            Colors.Add(entry);
        }
    }
}
