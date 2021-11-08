using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek.UnitTests;

public class ImageRgba32Assert
{
    public static void AreEqual(Image<Rgba32> expected, Image<Rgba32> actual)
    {
        Assert.AreEqual(expected.Width, actual.Width);
        Assert.AreEqual(expected.Height, actual.Height);

        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                Assert.AreEqual(expected[x, y], actual[x, y]);
            }
        }
    }
}
