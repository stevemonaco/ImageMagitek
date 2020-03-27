using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ImageMagitek.Colors.SerializationModels
{
    public class PaletteJsonModel
    {
        public string Name { get; set; }
        public List<string> Colors { get; set; }
        public bool ZeroIndexTransparent { get; set; } = true;

        public Palette ToPalette()
        {
            const string colorRegex = "^#([A-Fa-f0-9]){6,8}$";
            var regex = new Regex(colorRegex, RegexOptions.Compiled);

            var pal = new Palette(Name, ColorModel.RGBA32, 0, Colors.Count, ZeroIndexTransparent, PaletteStorageSource.Json);

            for (int i = 0; i < Colors.Count; i++)
            {
                if (regex.IsMatch(Colors[i]))
                    pal.SetNativeColor(i, HexStringToColorRgba32(Colors[i]));
            }

            return pal;
        }

        private ColorRgba32 HexStringToColorRgba32(string hexString)
        {
            if (hexString.Length == 7)
            {
                var R = byte.Parse(hexString.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber);
                var G = byte.Parse(hexString.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber);
                var B = byte.Parse(hexString.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber);
                return new ColorRgba32(R, G, B, 0xFF);
            }
            else if (hexString.Length == 9)
            {
                var R = byte.Parse(hexString.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber);
                var G = byte.Parse(hexString.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber);
                var B = byte.Parse(hexString.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber);
                var A = byte.Parse(hexString.AsSpan(7, 2), System.Globalization.NumberStyles.HexNumber);
                return new ColorRgba32(R, G, B, A);
            }
            else
                throw new NotSupportedException($"{nameof(HexStringToColorRgba32)} does not support strings of length {hexString.Length}");
        }
    }
}
