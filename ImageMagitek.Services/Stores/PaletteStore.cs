using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Colors;

namespace ImageMagitek.Services.Stores;

/// <summary>
/// Stores palettes that have been loaded from global sources. Does not store Project-specific palettes.
/// </summary>
public sealed class PaletteStore
{
    public List<Palette> GlobalPalettes { get; }
    public Palette? NesPalette { get; set; }

    private Palette _defaultPalette;
    public Palette DefaultPalette
    {
        get => _defaultPalette;
        set
        {
            if (!GlobalPalettes.Contains(value))
                GlobalPalettes.Add(value);

            _defaultPalette = value;
        }
    }

    public PaletteStore(Palette defaultPalette, IEnumerable<Palette> globalPalettes)
    {
        GlobalPalettes = globalPalettes.ToList();
        DefaultPalette = defaultPalette;
        _defaultPalette = defaultPalette;
    }

    public PaletteStore(Palette defaultPalette, Palette nesPalette, IEnumerable<Palette> globalPalettes)
    {
        GlobalPalettes = globalPalettes.ToList();
        DefaultPalette = defaultPalette;
        _defaultPalette = defaultPalette;

        NesPalette = nesPalette;
    }
}