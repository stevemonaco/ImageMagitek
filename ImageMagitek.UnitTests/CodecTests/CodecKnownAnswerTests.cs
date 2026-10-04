using System;
using System.Linq;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Hand-written encodings of small tiles, taken from platform documentation unless the test name says otherwise.
/// Row 0 holds indices 0..7 masked to the color depth and row 1 holds (max - x), unless the test builds its own rows.
/// </summary>
[Collection("Codec")]
public class CodecKnownAnswerTests
{
    private readonly CodecFixture _fixture;

    public CodecKnownAnswerTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Snes2Bpp_RowInterlacedPlanes() =>
        AssertKnownAnswer("SNES 2bpp", 8, 8, DepthTile(8, 8, 2), Pad(16, 0x55, 0x33, 0xAA, 0xCC));

    [Fact]
    public void Nes1Bpp_OneBytePerRow() =>
        AssertKnownAnswer("NES 1bpp", 8, 8, Tile(8, 8, [0, 1, 0, 1, 0, 1, 0, 1], [1, 1, 1, 0, 0, 0, 0, 0]), Pad(8, 0x55, 0xE0));

    [Fact]
    public void Nes2Bpp_SequentialPlanes() =>
        AssertKnownAnswer("NES 2bpp", 8, 8, DepthTile(8, 8, 2), [.. Pad(8, 0x55, 0xAA), .. Pad(8, 0x33, 0xCC)]);

    [Fact]
    public void Snes3BppFlow_InterlacedPlanesThenThirdPlane() =>
        AssertKnownAnswer("SNES 3bpp Flow", 8, 8, DepthTile(8, 8, 3), [.. Pad(16, 0x55, 0x33, 0xAA, 0xCC), .. Pad(8, 0x0F, 0xF0)]);

    [Fact]
    public void Snes4Bpp_TwoInterlacedPlanePairs() =>
        AssertKnownAnswer("SNES 4bpp", 8, 8, DepthTile(8, 8, 4), Snes4BppExpected);

    [Fact]
    public void Snes4BppPattern_MatchesSnes4BppLayout() =>
        AssertKnownAnswer("SNES4bpp Pattern", 8, 8, DepthTile(8, 8, 4), Snes4BppExpected);

    [Fact]
    public void Snes8Bpp_FourInterlacedPlanePairs() =>
        AssertKnownAnswer("SNES 8bpp", 8, 8, DepthTile(8, 8, 8),
            [.. Pad(16, 0x55, 0x33, 0xAA, 0xCC), .. Pad(16, 0x0F, 0x00, 0xF0, 0xFF), .. Pad(16, 0x00, 0x00, 0xFF, 0xFF), .. Pad(16, 0x00, 0x00, 0xFF, 0xFF)]);

    [Fact]
    public void GameGear4Bpp_FourPlanesPerRow() =>
        AssertKnownAnswer("Game Gear 4bpp", 8, 8, DepthTile(8, 8, 4), Pad(32, 0x55, 0x33, 0x0F, 0x00, 0xAA, 0xCC, 0xF0, 0xFF));

    [Fact]
    public void Genesis4Bpp_HighNibbleIsLeftPixel() =>
        AssertKnownAnswer("Genesis 4bpp", 8, 8, DepthTile(8, 8, 4), Pad(32, 0x01, 0x23, 0x45, 0x67, 0xFE, 0xDC, 0xBA, 0x98));

    [Fact]
    public void Gba4Bpp_LowNibbleIsLeftPixel() =>
        AssertKnownAnswer("GBA 4bpp", 8, 8, DepthTile(8, 8, 4), Gba4BppExpected);

    [Fact]
    public void Gba4BppPattern_LowNibbleIsLeftPixel() =>
        AssertKnownAnswer("GBA4bpp Pattern", 8, 8, DepthTile(8, 8, 4), Gba4BppExpected);

    [Fact]
    public void Gba8Bpp_OneBytePerPixel() =>
        AssertKnownAnswer("GBA 8bpp", 8, 8, DepthTile(8, 8, 8), BytePerPixelExpected);

    [Fact]
    public void Psx4BppFlow_LowNibbleIsLeftPixel() =>
        AssertKnownAnswer("PSX 4bpp Flow", 8, 8, DepthTile(8, 8, 4), Gba4BppExpected);

    [Fact]
    public void Psx8BppFlow_OneBytePerPixel() =>
        AssertKnownAnswer("PSX 8bpp Flow", 8, 8, DepthTile(8, 8, 8), BytePerPixelExpected);

    [Fact]
    public void SnesMode7_OneBytePerPixel() =>
        AssertKnownAnswer("SNES Mode7", 8, 8, DepthTile(8, 8, 8), BytePerPixelExpected);

    [Fact]
    public void VirtualBoy2Bpp_LittleEndianRowWordLeftPixelInLowBits() =>
        AssertKnownAnswer("Virtual Boy 2bpp", 8, 8, DepthTile(8, 8, 2), Pad(16, 0xE4, 0xE4, 0x1B, 0x1B));

    [Fact]
    public void NeoGeoPocket2Bpp_LittleEndianRowWordLeftPixelInHighBits() =>
        AssertKnownAnswer("NeoGeo Pocket 2bpp", 8, 8, DepthTile(8, 8, 2), Pad(16, 0x1B, 0x1B, 0xE4, 0xE4));

    [Fact]
    public void CotMFont_FromXmlSemantics_PixelPairsSwapped() =>
        AssertKnownAnswer("CotM Font", 8, 8, Tile(8, 8, [0, 1, 0, 1, 0, 1, 0, 1], [1, 1, 1, 0, 0, 0, 0, 0]), Pad(8, 0xAA, 0xD0));

    [Fact]
    public void Ff5Font_FromXmlSemantics_OneBytePerRow() =>
        AssertKnownAnswer("FF5 Font", 8, 12, Tile(8, 12, [0, 1, 0, 1, 0, 1, 0, 1], [1, 1, 1, 0, 0, 0, 0, 0]), Pad(12, 0x55, 0xE0));

    [Fact]
    public void Ff5Pattern_FromXmlSemantics_LeftColumnThenRightColumn() =>
        AssertKnownAnswer("FF5 Pattern", 16, 12, Tile(16, 12,
            [0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1],
            [1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1]),
            [.. Pad(12, 0x55, 0xE0), .. Pad(12, 0x55, 0x00, 0x07)]);

    [Fact]
    public void Tokimemo1Bpp_FromXmlSemantics_RightHalfFirst() =>
        AssertKnownAnswer("Tokimemo 1bpp", 16, 14, Tile(16, 14,
            [0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1],
            [1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
            Pad(28, 0x55, 0x55, 0x00, 0xE0));

    private static byte[] Snes4BppExpected => [.. Pad(16, 0x55, 0x33, 0xAA, 0xCC), .. Pad(16, 0x0F, 0x00, 0xF0, 0xFF)];
    private static byte[] Gba4BppExpected => Pad(32, 0x10, 0x32, 0x54, 0x76, 0xEF, 0xCD, 0xAB, 0x89);
    private static byte[] BytePerPixelExpected => Pad(64, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0xFF, 0xFE, 0xFD, 0xFC, 0xFB, 0xFA, 0xF9, 0xF8);

    private void AssertKnownAnswer(string codecName, int width, int height, byte[,] pixels, byte[] expected)
    {
        var codec = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);

        Assert.Equal(CodecTestHelpers.ToHex(expected), CodecTestHelpers.ToHex(CodecTestHelpers.Encode(codec, el, pixels)));
        IndexedImageAssert.AreEqual(pixels, CodecTestHelpers.Decode(codec, el, expected));
    }

    private static byte[,] DepthTile(int width, int height, int colorDepth)
    {
        var mask = (1 << colorDepth) - 1;
        var row0 = Enumerable.Range(0, width).Select(x => (byte)(x & mask)).ToArray();
        var row1 = Enumerable.Range(0, width).Select(x => (byte)((mask - x) & mask)).ToArray();
        return Tile(width, height, row0, row1);
    }

    private static byte[,] Tile(int width, int height, params byte[][] rows)
    {
        var tile = new byte[height, width];

        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < width; x++)
                tile[y, x] = rows[y][x];

        return tile;
    }

    private static byte[] Pad(int length, params byte[] leading)
    {
        var result = new byte[length];
        Array.Copy(leading, result, leading.Length);
        return result;
    }
}
