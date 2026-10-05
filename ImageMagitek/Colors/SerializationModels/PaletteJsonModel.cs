using System.Collections.Generic;
using System.IO;
using ImageMagitek.Utility.Parsing;

namespace ImageMagitek.Colors.Serialization;

public class PaletteJsonModel
{
    public required string Name { get; init; }
    public required List<string> Colors { get; init; }
    public bool ZeroIndexTransparent { get; init; } = true;

    public Palette ToPalette(IColorFactory colorFactory)
    {
        var pal = new Palette(Name, colorFactory, ColorModel.Rgba32, ZeroIndexTransparent, PaletteStorageSource.GlobalJson);

        var colors = new List<IColorSource>();
        for (int i = 0; i < Colors.Count; i++)
        {
            if (!ColorParser.TryParse(Colors[i], ColorModel.Rgba32, out var color))
                throw new InvalidDataException($"Palette '{Name}' color {i} '{Colors[i]}' is not a valid #RRGGBB or #RRGGBBAA color");

            colors.Add(new ProjectNativeColorSource((ColorRgba32)color));
        }

        pal.SetColorSources(colors);

        return pal;
    }
}
