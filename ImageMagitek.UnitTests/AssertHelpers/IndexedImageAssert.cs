using Xunit.Sdk;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Compares palette indices rather than colors, so index changes between duplicate palette colors are caught.
/// </summary>
public static class IndexedImageAssert
{
    public static void AreEqual(byte[,] expected, byte[,] actual)
    {
        var height = expected.GetLength(0);
        var width = expected.GetLength(1);

        if (height != actual.GetLength(0) || width != actual.GetLength(1))
            throw new XunitException($"Image size differs. Expected {width}x{height}, actual {actual.GetLength(1)}x{actual.GetLength(0)}");

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (expected[y, x] != actual[y, x])
                    throw new XunitException($"Index differs at ({x}, {y}). Expected {expected[y, x]}, actual {actual[y, x]}");
            }
        }
    }

    public static void AreEqual(byte[] expected, byte[] actual, int width)
    {
        if (expected.Length != actual.Length)
            throw new XunitException($"Image length differs. Expected {expected.Length}, actual {actual.Length}");

        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
                throw new XunitException($"Index differs at ({i % width}, {i / width}). Expected {expected[i]}, actual {actual[i]}");
        }
    }
}
