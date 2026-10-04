using ImageMagitek.Codec;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Formats with two independent implementations must agree in both directions.
/// </summary>
[Collection("Codec")]
public class CodecEquivalenceTests
{
    private readonly CodecFixture _fixture;

    public CodecEquivalenceTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(8, 8)]
    [InlineData(16, 16)]
    [InlineData(16, 8)]
    [InlineData(8, 16)]
    public void Snes3BppFlow_MatchesSpecialized(int width, int height)
    {
        var flow = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "SNES 3bpp Flow", width, height);
        AssertEquivalent(flow, new Snes3BppCodec(flow.Palette, width, height));
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(128, 64)]
    [InlineData(16, 8)]
    public void Psx4BppFlow_MatchesSpecialized(int width, int height)
    {
        var flow = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "PSX 4bpp Flow", width, height);
        AssertEquivalent(flow, new Psx4BppCodec(flow.Palette, width, height));
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(128, 64)]
    [InlineData(16, 8)]
    public void Psx8BppFlow_MatchesSpecialized(int width, int height)
    {
        var flow = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "PSX 8bpp Flow", width, height);
        AssertEquivalent(flow, new Psx8BppCodec(flow.Palette, width, height));
    }

    [Fact]
    public void Snes4Bpp_MatchesPattern() =>
        AssertEquivalent(
            CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "SNES 4bpp", 8, 8),
            CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "SNES4bpp Pattern", 8, 8));

    [Fact]
    public void Gba4Bpp_MatchesPattern() =>
        AssertEquivalent(
            CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "GBA 4bpp", 8, 8),
            CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "GBA4bpp Pattern", 8, 8));

    [Theory]
    [InlineData(8, 8)]
    [InlineData(16, 8)]
    [InlineData(3, 3)]
    public void Nes1BppXml_MatchesSpecialized(int width, int height)
    {
        var flow = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "NES 1bpp", width, height);
        AssertEquivalent(flow, new Nes1BppCodec(flow.Palette, width, height));
    }

    [Fact]
    public void Ff5Font_TwoTilesSideBySide_MatchPatternTile()
    {
        var palette = TestImageGenerator.CreateDistinctPalette(1);
        var font = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1,
            (_, _) => CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "FF5 Font", 8, 12, palette));
        var pattern = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 1, 1,
            (_, _) => CodecTestHelpers.CreateCodec(_fixture.CodecFactory, "FF5 Pattern", 16, 12, palette));
        var fontSource = font.GetElement(0, 0)!.Value.Source;
        var patternSource = pattern.GetElement(0, 0)!.Value.Source;

        Assert.Equal(fontSource.Length, patternSource.Length);

        var data = TestImageGenerator.RandomBytes((int)fontSource.Length, 31);
        fontSource.Write(BitAddress.Zero, data);
        patternSource.Write(BitAddress.Zero, data);

        IndexedImageAssert.AreEqual(new IndexedImage(font).Image, new IndexedImage(pattern).Image, 16);

        var indices = TestImageGenerator.Flatten(TestImageGenerator.RandomIndices(16, 12, 1, 32));
        CodecTestHelpers.SaveIndices(font, indices);
        CodecTestHelpers.SaveIndices(pattern, indices);

        Assert.Equal(CodecTestHelpers.ReadAll(fontSource), CodecTestHelpers.ReadAll(patternSource));
    }

    private static void AssertEquivalent(IIndexedCodec first, IIndexedCodec second)
    {
        AssertDecodeEquivalent(first, second);

        var pixels = TestImageGenerator.RandomIndices(first.Width, first.Height, first.ColorDepth, 42);
        var firstEncoded = CodecTestHelpers.Encode(first, CodecTestHelpers.CreateElement(first), pixels);
        var secondEncoded = CodecTestHelpers.Encode(second, CodecTestHelpers.CreateElement(second), pixels);

        Assert.Equal(
            CodecTestHelpers.ToHex(CodecTestHelpers.MaskToStorageSize(firstEncoded, first.StorageSize)),
            CodecTestHelpers.ToHex(CodecTestHelpers.MaskToStorageSize(secondEncoded, second.StorageSize)));
    }

    private static void AssertDecodeEquivalent(IIndexedCodec first, IIndexedCodec second)
    {
        Assert.Equal((first.Width, first.Height, first.ColorDepth, first.StorageSize), (second.Width, second.Height, second.ColorDepth, second.StorageSize));

        var data = TestImageGenerator.RandomBytes((first.StorageSize + 7) / 8, 41);
        var firstDecoded = CodecTestHelpers.Decode(first, CodecTestHelpers.CreateElement(first), data);
        var secondDecoded = CodecTestHelpers.Decode(second, CodecTestHelpers.CreateElement(second), data);

        IndexedImageAssert.AreEqual(firstDecoded, secondDecoded);
    }
}
