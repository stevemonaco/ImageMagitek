using System;
using System.Collections.Generic;
using ImageMagitek.Colors;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

public partial class DirectCodecContractTests
{
    private static readonly Dictionary<string, Func<ColorRgba32, ColorRgba32>> _representable = new()
    {
        ["Bmp24"] = c => c with { A = 255 },
        ["Rgb24 Tiled"] = c => c with { A = 255 },
        ["PSX 24bpp"] = c => c with { A = 255 },
        ["PSX 16bpp"] = ToPsx16Representable,
        ["N64 Rgba16"] = c => new ColorRgba32((byte)(c.R & 0xF8), (byte)(c.G & 0xF8), (byte)(c.B & 0xF8), (byte)((c.A & 1) == 1 ? 255 : 0)),
    };

    public static TheoryData<string, int, int> ContractCases =>
        CodecTestHelpers.BuildCases(includeSquare: true, codecNames: CodecFixture.Shared.DirectCodecNames);

    /// <summary>
    /// Random colors reduced to the precision the codec stores, so they survive an encode and decode unchanged.
    /// </summary>
    public static ColorRgba32[,] RepresentableColors(string codecName, int width, int height, uint seed)
    {
        var colors = TestImageGenerator.RandomColors(width, height, seed);

        if (_representable.TryGetValue(codecName, out var normalize))
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    colors[y, x] = normalize(colors[y, x]);
        }

        return colors;
    }

    private static ColorRgba32 ToPsx16Representable(ColorRgba32 c)
    {
        var rgb = new ColorRgba32((byte)(c.R & 0xF8), (byte)(c.G & 0xF8), (byte)(c.B & 0xF8), 255);
        var isBlack = (rgb.R | rgb.G | rgb.B) == 0;

        if (c.A < 64)
            return new ColorRgba32(0, 0, 0, 0);
        if (c.A < 192 && !isBlack)
            return rgb with { A = 128 };
        return rgb;
    }
}
