using System;
using System.Text.RegularExpressions;
using ImageMagitek.Colors;

namespace ImageMagitek.Utility.Parsing
{
    public static class ColorParser
    {
        private const string _nativeColorRegexString = "^#([A-Fa-f0-9]){6,8}$";
        private const string _nesColorRegexString = "^#([A-Fa-f0-9]){2}$";
        private const string _twoByteColorRegexString = "^#([A-Fa-f0-9]){4}$";

        private static readonly Regex _nativeRegex = new Regex(_nativeColorRegexString, RegexOptions.Compiled);
        private static readonly Regex _nesRegex = new Regex(_nesColorRegexString, RegexOptions.Compiled);
        private static readonly Regex _twoByteRegex = new Regex(_twoByteColorRegexString, RegexOptions.Compiled);

        /// <summary>
        /// Tries to parse a color represented as a hexadecimal string
        /// </summary>
        /// <param name="input">Hexadecimal string</param>
        /// <param name="colorModel">Color to parse as</param>
        /// <param name="color">Result</param>
        /// <returns>True if successful, false if failed</returns>
        public static bool TryParse(string input, ColorModel colorModel, out IColor color)
        {
            if (colorModel == ColorModel.Rgba32)
            {
                if (_nativeRegex.IsMatch(input))
                {
                    color = NativeHexStringToColorRgba32(input);
                    return true;
                }
            }
            else if (colorModel == ColorModel.Bgr15)
            {
                if (_twoByteRegex.IsMatch(input))
                {
                    var a = byte.Parse(input.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber);
                    var b = byte.Parse(input.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber);
                    uint bgr15Raw = (uint)(a << 8) | b;
                    color = new ColorBgr15(bgr15Raw);
                    return true;
                }
            }
            else if (colorModel == ColorModel.Abgr16)
            {
                if (_twoByteRegex.IsMatch(input))
                {
                    var a = byte.Parse(input.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber);
                    var b = byte.Parse(input.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber);
                    uint abgr16Raw = (uint)(a << 8) | b;
                    color = new ColorAbgr16(abgr16Raw);
                    return true;
                }
            }
            else if (colorModel == ColorModel.Nes)
            {
                if (_nesRegex.IsMatch(input))
                {
                    uint nesRaw = byte.Parse(input.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber);
                    color = new ColorNes(nesRaw);
                    return true;
                }
            }

            color = default;
            return false;
        }

        private static ColorRgba32 NativeHexStringToColorRgba32(string hexString)
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
                throw new NotSupportedException($"{nameof(NativeHexStringToColorRgba32)} does not support strings of length {hexString.Length}");
        }
    }
}
