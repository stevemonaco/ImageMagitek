using ImageMagitek.Colors;
using Xunit.Sdk;

namespace ImageMagitek.UnitTests;

public static class ColorImageAssert
{
    public static void AreEqual(ColorRgba32[,] expected, ColorRgba32[,] actual)
    {
        var height = expected.GetLength(0);
        var width = expected.GetLength(1);

        if (height != actual.GetLength(0) || width != actual.GetLength(1))
            throw new XunitException($"Image size differs. Expected {width}x{height}, actual {actual.GetLength(1)}x{actual.GetLength(0)}");

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (expected[y, x].Color != actual[y, x].Color)
                    throw new XunitException($"Color differs at ({x}, {y}). Expected {Format(expected[y, x])}, actual {Format(actual[y, x])}");
            }
        }
    }

    public static void AreEqual(ColorRgba32[] expected, ColorRgba32[] actual, int width)
    {
        if (expected.Length != actual.Length)
            throw new XunitException($"Image length differs. Expected {expected.Length}, actual {actual.Length}");

        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i].Color != actual[i].Color)
                throw new XunitException($"Color differs at ({i % width}, {i / width}). Expected {Format(expected[i])}, actual {Format(actual[i])}");
        }
    }

    private static string Format(ColorRgba32 c) => $"RGBA({c.R:X2}, {c.G:X2}, {c.B:X2}, {c.A:X2})";
}
