using System;
using System.Text.RegularExpressions;
using ImageMagitek.Colors;

namespace ImageMagitek.Utility.Parsing
{
    public static class ForeignColorParser
    {
        const string _colorRegex = "^#([A-Fa-f0-9]){2,4,6,8}$";
        private static readonly Regex _regex = new Regex(_colorRegex, RegexOptions.Compiled);

        public static bool TryParse(string input, ColorModel colorModel, out IColor32 color)
        {
            if (_regex.IsMatch(input))
            {
                color = HexStringToColorRgba32(input, colorModel);
                return true;
            }

            color = default;
            return false;
        }

        private static ColorRgba32 HexStringToColorRgba32(string hexString, ColorModel colorModel)
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
