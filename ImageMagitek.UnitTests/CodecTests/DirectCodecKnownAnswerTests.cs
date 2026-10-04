using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Hand-written pixels and bytes for each direct-color format, taken from platform documentation.
/// </summary>
[Collection("Codec")]
public class DirectCodecKnownAnswerTests
{
    private const string N64Rgba32ByteOrderBug =
        "N64 Rgba32 reads and writes G,R,A,B (16-bit byte-swapped) instead of R,G,B,A; see CodecRework findings";

    private static readonly ColorRgba32 _opaqueBlack = new(0, 0, 0, 255);
    private static readonly ColorRgba32 _transparent = new(0, 0, 0, 0);

    private readonly CodecFixture _fixture;

    public DirectCodecKnownAnswerTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Rgba32Tiled_RgbaPerPixel() =>
        AssertKnownAnswer("Rgba32 Tiled", 8, 8,
            Tile(8, 8, _transparent, (0, 0, new(0x12, 0x34, 0x56, 0xFF)), (1, 0, new(0xAB, 0xCD, 0xEF, 0x80))),
            Bytes(256, (0, [0x12, 0x34, 0x56, 0xFF, 0xAB, 0xCD, 0xEF, 0x80])));

    [Fact]
    public void Rgb24Tiled_RgbPerPixel() =>
        AssertKnownAnswer("Rgb24 Tiled", 8, 8, Rgb24Tile, Rgb24Expected);

    [Fact]
    public void Psx24Bpp_RgbPerPixel() =>
        AssertKnownAnswer("PSX 24bpp", 8, 8, Rgb24Tile, Rgb24Expected);

    [Fact]
    public void Bmp24_BgrPerPixel_RowsBottomUp() =>
        AssertKnownAnswer("Bmp24", 8, 8,
            Tile(8, 8, _opaqueBlack, (0, 0, new(0x12, 0x34, 0x56, 0xFF)), (1, 0, new(0xAB, 0xCD, 0xEF, 0xFF)), (0, 7, new(0x01, 0x02, 0x03, 0xFF))),
            Bytes(192, (0, [0x03, 0x02, 0x01]), (7 * 24, [0x56, 0x34, 0x12, 0xEF, 0xCD, 0xAB])));

    [Fact]
    public void Psx16Bpp_LittleEndianBgr555_OpaqueColorsClearStp() =>
        AssertKnownAnswer("PSX 16bpp", 8, 8, Psx16Tile, Psx16Expected);

    [Fact]
    public void Psx16Bpp_TransparentAndSemiTransparent()
    {
        var tile = Tile(8, 8, _transparent, (0, 0, new(0xF8, 0x00, 0x00, 0x80)), (1, 0, _opaqueBlack));
        var expected = Bytes(128, (0, [0x1F, 0x80, 0x00, 0x80]));

        AssertKnownAnswer("PSX 16bpp", 8, 8, tile, expected);
    }

    [Fact]
    public void N64Rgba16_BigEndian5551_Stores16BitsPerPixel()
    {
        var codec = CodecTestHelpers.CreateDirectCodec(_fixture.CodecFactory, "N64 Rgba16", 32, 32);

        Assert.Equal(16, codec.ColorDepth);
        Assert.Equal(16 * 32 * 32, codec.StorageSize);
        AssertKnownAnswer("N64 Rgba16", 32, 32, N64Rgba16Tile, N64Rgba16Expected);
    }

    [Fact(Skip = N64Rgba32ByteOrderBug)]
    public void N64Rgba32_RgbaPerPixel() =>
        AssertKnownAnswer("N64 Rgba32", 32, 32,
            Tile(32, 32, _transparent, (0, 0, new(0x12, 0x34, 0x56, 0x78)), (1, 0, new(0xAB, 0xCD, 0xEF, 0x01))),
            Bytes(4096, (0, [0x12, 0x34, 0x56, 0x78, 0xAB, 0xCD, 0xEF, 0x01])));

    private static ColorRgba32[,] Rgb24Tile => Tile(8, 8, _opaqueBlack, (0, 0, new(0x12, 0x34, 0x56, 0xFF)), (1, 0, new(0xAB, 0xCD, 0xEF, 0xFF)));
    private static byte[] Rgb24Expected => Bytes(192, (0, [0x12, 0x34, 0x56, 0xAB, 0xCD, 0xEF]));

    private static ColorRgba32[,] Psx16Tile => Tile(8, 8, _opaqueBlack,
        (0, 0, new(0xF8, 0x00, 0x00, 0xFF)), (1, 0, new(0x00, 0xF8, 0x00, 0xFF)), (2, 0, new(0x00, 0x00, 0xF8, 0xFF)), (3, 0, new(0x08, 0x10, 0x18, 0xFF)));

    // Opaque black must set STP, since 0x0000 is the transparent color
    private static byte[] Psx16Expected
    {
        get
        {
            var bytes = Bytes(128, (0, [0x1F, 0x00, 0xE0, 0x03, 0x00, 0x7C, 0x41, 0x0C]));
            for (int i = 9; i < bytes.Length; i += 2)
                bytes[i] = 0x80;
            return bytes;
        }
    }

    private static ColorRgba32[,] N64Rgba16Tile => Tile(32, 32, _transparent,
        (0, 0, new(0xF8, 0x00, 0x00, 0xFF)), (1, 0, new(0x00, 0xF8, 0x00, 0xFF)), (2, 0, new(0x00, 0x00, 0xF8, 0xFF)), (3, 0, new(0x08, 0x10, 0x18, 0x00)));
    private static byte[] N64Rgba16Expected => Bytes(2048, (0, [0xF8, 0x01, 0x07, 0xC1, 0x00, 0x3F, 0x08, 0x86]));

    private void AssertKnownAnswer(string codecName, int width, int height, ColorRgba32[,] pixels, byte[] expected)
    {
        var codec = CodecTestHelpers.CreateDirectCodec(_fixture.CodecFactory, codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);

        Assert.Equal(CodecTestHelpers.ToHex(expected), CodecTestHelpers.ToHex(CodecTestHelpers.Encode(codec, el, pixels)));
        ColorImageAssert.AreEqual(pixels, CodecTestHelpers.Decode(codec, el, expected));
    }

    private static ColorRgba32[,] Tile(int width, int height, ColorRgba32 fill, params (int X, int Y, ColorRgba32 Color)[] pixels)
    {
        var tile = new ColorRgba32[height, width];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                tile[y, x] = fill;

        foreach (var (x, y, color) in pixels)
            tile[y, x] = color;

        return tile;
    }

    private static byte[] Bytes(int length, params (int Offset, byte[] Data)[] runs)
    {
        var result = new byte[length];

        foreach (var (offset, data) in runs)
            data.CopyTo(result, offset);

        return result;
    }
}
