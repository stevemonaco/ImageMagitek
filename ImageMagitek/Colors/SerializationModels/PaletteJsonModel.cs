using System.Collections.Generic;
using ImageMagitek.Utility.Parsing;

namespace ImageMagitek.Colors.Serialization
{
    public class PaletteJsonModel
    {
        public string Name { get; set; }
        public List<string> Colors { get; set; }
        public bool ZeroIndexTransparent { get; set; } = true;

        public Palette ToPalette(IColorFactory colorFactory)
        {
            var pal = new Palette(Name, colorFactory, ColorModel.Rgba32, ZeroIndexTransparent, PaletteStorageSource.Json);

            var colors = new List<IColorSource>();
            for (int i = 0; i < Colors.Count; i++)
            {
                if (ColorParser.TryParse(Colors[i], ColorModel.Rgba32, out var color))
                    colors.Add(new ProjectNativeColorSource((ColorRgba32)color));
            }

            pal.SetColorSources(colors);

            return pal;
        }
    }
}
