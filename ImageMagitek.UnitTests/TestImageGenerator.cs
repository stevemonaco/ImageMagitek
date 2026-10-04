using System.Linq;
using ImageMagitek.Colors;
using ImageMagitek.UnitTests.TestFactories;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Deterministic test data. Uses its own PRNG rather than System.Random so snapshots never move with a runtime upgrade.
/// </summary>
public static class TestImageGenerator
{
    public static byte[,] RandomIndices(int width, int height, int colorDepth, uint seed)
    {
        var rng = new XorShift32(seed);
        var mask = (1 << colorDepth) - 1;
        var indices = new byte[height, width];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                indices[y, x] = (byte)(rng.Next() & mask);

        return indices;
    }

    public static byte[,] GradientIndices(int width, int height, int colorDepth)
    {
        var colors = 1 << colorDepth;
        var indices = new byte[height, width];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                indices[y, x] = (byte)((x + y * 3) % colors);

        return indices;
    }

    public static byte[] RandomBytes(int length, uint seed)
    {
        var rng = new XorShift32(seed);
        var bytes = new byte[length];

        for (int i = 0; i < length; i++)
            bytes[i] = (byte)rng.Next();

        return bytes;
    }

    /// <summary>
    /// Creates a palette of 2^colorDepth distinct opaque colors so exact-match import is unambiguous.
    /// </summary>
    public static Palette CreateDistinctPalette(int colorDepth)
    {
        var count = 1 << colorDepth;
        var colors = Enumerable.Range(0, count)
            .Select(i => new ColorRgba32((byte)i, (byte)(i * 53), (byte)(i * 101), 255))
            .ToArray();

        return ArrangerTestFactory.CreatePalette(colors);
    }

    public static byte[] Flatten(byte[,] indices) => indices.Cast<byte>().ToArray();

    private struct XorShift32(uint seed)
    {
        private uint _state = seed;

        public uint Next()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x >> 8;
        }
    }
}
