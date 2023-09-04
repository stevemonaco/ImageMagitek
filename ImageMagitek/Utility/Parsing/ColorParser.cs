using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ImageMagitek.Colors;

namespace ImageMagitek.Utility.Parsing;

public static partial class ColorParser
{
    private const string _nativeColorRegexString    = @"^#([A-Fa-f0-9]{2}){3,4}$";
    private const string _nesColorRegexString       = @"^#[A-Fa-f0-9]{2}$";
    private const string _twoByteColorRegexString   = @"^#[A-Fa-f0-9]{4}$";
    private const string _oneByteColorRegexString   = @"^#[A-Fa-f0-9]{2}$";

    [GeneratedRegex(_nativeColorRegexString, RegexOptions.Compiled, "en-US")]
    private static partial Regex NativeRegex();

    [GeneratedRegex(_nesColorRegexString, RegexOptions.Compiled, "en-US")]
    private static partial Regex NesRegex();

    [GeneratedRegex(_twoByteColorRegexString, RegexOptions.Compiled, "en-US")]
    private static partial Regex TwoByteRegex();

    [GeneratedRegex(_oneByteColorRegexString, RegexOptions.Compiled, "en-US")]
    private static partial Regex OneByteRegex();

    /// <summary>
    /// Tries to parse a color represented as a hexadecimal string
    /// </summary>
    /// <param name="input">Hexadecimal string</param>
    /// <param name="colorModel">Color to parse as</param>
    /// <param name="color">Result</param>
    /// <returns>True if successful, false if failed</returns>
    public static bool TryParse(string input, ColorModel colorModel, [MaybeNullWhen(false)] out IColor color)
    {
        if (colorModel == ColorModel.Rgba32)
        {
            if (NativeRegex().IsMatch(input))
            {
                color = NativeHexStringToColorRgba32(input);
                return true;
            }
        }
        else if (colorModel == ColorModel.Rgb15)
        {
            if (TwoByteRegex().IsMatch(input))
            {
                var a = byte.Parse(input.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber);
                var b = byte.Parse(input.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber);
                uint rgb15Raw = (uint)(a << 8) | b;
                color = new ColorBgr15(rgb15Raw);
                return true;
            }
        }
        else if (colorModel == ColorModel.Bgr15)
        {
            if (TwoByteRegex().IsMatch(input))
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
            if (TwoByteRegex().IsMatch(input))
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
            if (NesRegex().IsMatch(input))
            {
                uint nesRaw = byte.Parse(input.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber);
                color = new ColorNes(nesRaw);
                return true;
            }
        }
        else if (colorModel == ColorModel.Bgr9)
        {
            if (TwoByteRegex().IsMatch(input))
            {
                var a = byte.Parse(input.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber);
                var b = byte.Parse(input.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber);
                uint bgr9Raw = (uint)(a << 8) | b;
                color = new ColorBgr9(bgr9Raw);
                return true;
            }
        }
        else if (colorModel == ColorModel.Bgr6)
        {
            if (OneByteRegex().IsMatch(input))
            {
                uint bgr6Raw = byte.Parse(input.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber);
                color = new ColorBgr6(bgr6Raw);
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
            var r = byte.Parse(hexString.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber);
            var g = byte.Parse(hexString.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber);
            var b = byte.Parse(hexString.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber);
            return new ColorRgba32(r, g, b, 0xFF);
        }
        else if (hexString.Length == 9)
        {
            var r = byte.Parse(hexString.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber);
            var g = byte.Parse(hexString.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber);
            var b = byte.Parse(hexString.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber);
            var a = byte.Parse(hexString.AsSpan(7, 2), System.Globalization.NumberStyles.HexNumber);
            return new ColorRgba32(r, g, b, a);
        }
        else
            throw new NotSupportedException($"{nameof(NativeHexStringToColorRgba32)} does not support strings of length {hexString.Length}");
    }
}
