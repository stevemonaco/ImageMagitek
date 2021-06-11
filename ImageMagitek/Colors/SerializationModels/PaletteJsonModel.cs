using System.Collections.Generic;
using ImageMagitek.Utility.Parsing;

namespace ImageMagitek.Colors.SerializationModels
{
    public class PaletteJsonModel
    {
        public string Name { get; set; }
        public List<string> Colors { get; set; }
        public bool ZeroIndexTransparent { get; set; } = true;

        public Palette ToPalette(IColorFactory colorFactory)
        {
            var pal = new Palette(Name, colorFactory, ColorModel.Rgba32, Colors.Count, ZeroIndexTransparent, PaletteStorageSource.Json);

            for (int i = 0; i < Colors.Count; i++)
            {
                if (NativeColorParser.TryParse(Colors[i], out var color))
                    pal.SetNativeColor(i, color);
            }

            return pal;
        }
    }
}
