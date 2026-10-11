using System;
using ImageMagitek.Colors;

namespace ImageMagitek.UnitTests;

/// <summary>
/// sRGB to OKLab conversion (https://bottosson.github.io/posts/oklab/) for perceptual palette checks.
/// </summary>
public static class OkLab
{
    public static (double L, double A, double B) ToOkLab(ColorRgba32 color)
    {
        var r = ToLinear(color.R);
        var g = ToLinear(color.G);
        var b = ToLinear(color.B);

        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

        return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    public static double DeltaE(ColorRgba32 first, ColorRgba32 second)
    {
        var (l1, a1, b1) = ToOkLab(first);
        var (l2, a2, b2) = ToOkLab(second);
        return Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
    }

    private static double ToLinear(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
