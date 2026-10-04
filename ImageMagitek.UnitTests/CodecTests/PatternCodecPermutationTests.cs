using System.Linq;
using ImageMagitek.Codec;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Pattern codecs whose merge priority and row pixel pattern are not their own inverse, which no shipped XML exercises.
/// </summary>
public class PatternCodecPermutationTests
{
    private static IndexedPatternGraphicsCodec CreateCodec()
    {
        var pattern = PatternList.TryCreatePatternList(["AAAAAAAA", "BBBBBBBB", "CCCCCCCC"], PixelPacking.Planar, 8, 8, 3, 24).AsSuccess.Result;
        var format = new PatternGraphicsFormat("Permuted 3bpp", PixelColorType.Indexed, 3, ImageLayout.Tiled, PixelPacking.Planar,
            8, 8, [2, 0, 1], new RepeatList([1, 2, 3, 0, 5, 6, 7, 4]), pattern);

        return new IndexedPatternGraphicsCodec(format, TestImageGenerator.CreateDistinctPalette(3));
    }

    [Fact]
    public void Decode_FirstBit_LandsAtPermutedPixelAndColorBit()
    {
        var codec = CreateCodec();
        var el = CodecTestHelpers.CreateElement(codec);
        var encoded = new byte[(codec.StorageSize + 7) / 8];
        encoded[0] = 0x80;

        var pixels = CodecTestHelpers.Decode(codec, el, encoded);

        Assert.Equal(4, pixels[0, 1]);
        Assert.Equal(4, TestImageGenerator.Flatten(pixels).Sum(x => x));
    }

    [Fact]
    public void Encode_IsInverseOfDecode_ForSingleBit()
    {
        var codec = CreateCodec();
        var el = CodecTestHelpers.CreateElement(codec);
        var pixels = new byte[8, 8];
        pixels[0, 1] = 4;

        var encoded = CodecTestHelpers.Encode(codec, el, pixels);

        Assert.Equal(0x80, encoded[0]);
        Assert.All(encoded[1..], x => Assert.Equal(0, x));
    }

    [Fact]
    public void PixelsToBytesToPixels_RoundTrips()
    {
        var codec = CreateCodec();
        var el = CodecTestHelpers.CreateElement(codec);
        var pixels = TestImageGenerator.RandomIndices(8, 8, 3, 7);

        var encoded = CodecTestHelpers.Encode(codec, el, pixels);

        Assert.Equal(pixels, CodecTestHelpers.Decode(codec, el, encoded));
    }

    [Fact]
    public void BytesToPixelsToBytes_RoundTrips()
    {
        var codec = CreateCodec();
        var el = CodecTestHelpers.CreateElement(codec);
        var bytes = TestImageGenerator.RandomBytes((codec.StorageSize + 7) / 8, 9);

        var pixels = CodecTestHelpers.Decode(codec, el, bytes);

        Assert.Equal(bytes, CodecTestHelpers.Encode(codec, el, pixels));
    }
}
