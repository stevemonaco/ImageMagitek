using System;
using ColorMine.ColorSpaces;
using ColorMine.ColorSpaces.Comparisons;

namespace ImageMagitek.Colors;

/// <summary>
/// Color distance metrics used by palette matching
/// </summary>
internal static class ColorDistance
{
    private static readonly Cie94Comparison _cie94 = new(Cie94Comparison.Application.GraphicArts);

    public static Lab ToLab(ColorRgba32 color) =>
        new Rgb { R = color.R, G = color.G, B = color.B }.To<Lab>();

    public static double Cie94(Lab a, Lab b) => _cie94.Compare(a, b);

    /// <summary>
    /// Euclidean RGB distance weighted by mean red so it tracks perception better than plain RGB
    /// </summary>
    public static double WeightedRgb(ColorRgba32 a, ColorRgba32 b)
    {
        var rMean = (a.R + b.R) / 2.0;
        double dr = a.R - b.R;
        double dg = a.G - b.G;
        double db = a.B - b.B;

        return Math.Sqrt((2 + rMean / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rMean) / 256) * db * db);
    }
}
