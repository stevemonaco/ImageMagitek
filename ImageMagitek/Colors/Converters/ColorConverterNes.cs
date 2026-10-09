using System;

namespace ImageMagitek.Colors.Converters;

public class ColorConverterNes : IColorConverter<ColorNes>
{
    private readonly Palette _nesPalette;

    public ColorConverterNes(Palette nesPalette)
    {
        _nesPalette = nesPalette;
    }

    public ColorNes ToForeignColor(ColorRgba32 nc)
    {
        // A matcher per call: its cache is not thread-safe and this converter is shared through ColorFactory
        var matcher = new PaletteColorMatcher(_nesPalette, ColorMatchStrategy.Nearest, entryLimit: 64);
        if (matcher.TryMatch(nc, out var match))
        {
            return new ColorNes(match.Index);
        }
        throw new ArgumentException($"{nameof(ToForeignColor)} parameter (R: {nc.R}, G: {nc.G}, B: {nc.B}, A: {nc.A}) could not be matched in palette '{_nesPalette.Name}'");
    }

    public ColorRgba32 ToNativeColor(ColorNes fc)
    {
        return _nesPalette.GetNativeColor((int)fc.Color);
    }
}
