using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.PluginSamples;
using ImageMagitek.UnitTests.TestFactories;
using TileShop.UI.Features.Graphics;
using Xunit;

namespace ImageMagitek.UnitTests.Graphics;

public class ArrangerSkiaBitmapTests
{
    private const int ElementSize = 8;
    private const int ElemsX = 5;
    private const int ElemsY = 5;
    private const int ElementsInSource = 20;

    [Fact]
    public void FullImage_MatchesPerPixelReference()
    {
        var image = new IndexedImage(CreateArranger());
        image.GetPixelSpan()[^(ElementSize * image.Width)..].Fill(5);

        using var bitmap = new ArrangerSkiaBitmap(image);

        AssertMatchesReference(image, bitmap);
    }

    [Fact]
    public void SubsectionOffElementBoundary_MatchesPerPixelReference()
    {
        var image = new IndexedImage(CreateArranger(), 3, 2, 30, 20);

        using var bitmap = new ArrangerSkiaBitmap(image);

        AssertMatchesReference(image, bitmap);
    }

    [Fact]
    public void PartialInvalidateMidElement_MatchesPerPixelReference()
    {
        var image = new IndexedImage(CreateArranger());
        using var bitmap = new ArrangerSkiaBitmap(image);
        var (left, top, width, height) = (5, 3, 13, 6);

        var pixels = image.GetPixelSpan();
        for (int y = top; y < top + height; y++)
        {
            for (int x = left; x < left + width; x++)
                pixels[x + y * image.Width] = (byte)((pixels[x + y * image.Width] + 3) % 16);
        }
        bitmap.Invalidate(left, top, width, height);

        AssertMatchesReference(image, bitmap);
    }

    private static ScatteredArranger CreateArranger()
    {
        var palettes = new[]
        {
            CreatePalette(16, 0),
            CreatePalette(16, 40),
            CreatePalette(8, 90),
        };
        palettes[1].ZeroIndexTransparent = true;

        var bytesPerElement = ElementSize * ElementSize / 2;
        var bytes = Enumerable.Range(0, ElementsInSource * bytesPerElement).Select(i => (byte)(i * 37 + 11)).ToArray();
        var source = new MemoryDataSource("test", bytes.Length);
        source.Write(new BitAddress(0), bytes);

        return ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, ElemsX, ElemsY,
            (x, y) => new IndexedCodecPluginAdapter(new Psx4BppCodec(), palettes[(x + y) % palettes.Length], ElementSize, ElementSize), source);
    }

    private static Palette CreatePalette(int entries, int seed) =>
        ArrangerTestFactory.CreatePalette(Enumerable.Range(0, entries)
            .Select(i => new ColorRgba32((byte)(seed + i * 8), (byte)(seed + i * 4), (byte)(i * 2), (byte)(200 + i)))
            .ToArray());

    private static uint ReferenceColor(IndexedImage image, int x, int y)
    {
        var index = image.GetPixel(x, y);
        if (image.GetElementAtPixel(x, y) is not { IsWithinSource: true, Codec: IIndexedCodec codec } || index >= codec.Palette.Entries)
            return 0;

        var color = codec.Palette[index].Color;
        var output = (color & 0xFF00FF00) | BitOperations.RotateLeft(color & 0xFF00FF, 16);
        return index == 0 && codec.Palette.ZeroIndexTransparent ? output & 0x00FFFFFF : output;
    }

    private static void AssertMatchesReference(IndexedImage image, ArrangerSkiaBitmap bitmap)
    {
        var actual = MemoryMarshal.Cast<byte, uint>(bitmap.Bitmap.GetPixelSpan());
        var stride = bitmap.Bitmap.RowBytes / 4;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
                Assert.True(ReferenceColor(image, x, y) == actual[x + y * stride], $"Pixel ({x}, {y}) differs");
        }
    }
}
