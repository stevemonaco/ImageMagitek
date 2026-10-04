using System.Drawing;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Round-trips through a <see cref="SequentialArranger"/> at a nonzero offset. Restricted to tiled codecs because
/// single-layout element addressing is not bit-wise (see the TODO in SequentialArranger.PerformLayout).
/// </summary>
[Collection("Codec")]
public class SequentialArrangerRoundTripTests
{
    private const int _elementsX = 4;
    private const int _elementsY = 3;
    private const int _offsetBytes = 37;

    private readonly CodecFixture _fixture;

    public SequentialArrangerRoundTripTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string> RepresentativeCodecs =>
    [
        "NES 1bpp", "NES 2bpp", "SNES 2bpp", "SNES 3bpp Flow", "SNES 4bpp", "SNES 8bpp", "Game Gear 4bpp", "Genesis 4bpp",
        "GBA 4bpp", "GBA 8bpp", "SNES Mode7", "Virtual Boy 2bpp", "NeoGeo Pocket 2bpp", "CotM Font", "FF5 Font", "Tokimemo 1bpp",
        "SNES4bpp Pattern", "GBA4bpp Pattern", "FF5 Pattern",
    ];

    [Theory]
    [MemberData(nameof(RepresentativeCodecs))]
    public void RoundTrips_AtNonzeroOffset_AndAfterCodecResize(string codecName)
    {
        var codec = _fixture.CodecFactory.CreateCodec(codecName)!;
        var palette = TestImageGenerator.CreateDistinctPalette(codec.ColorDepth);
        var maxElementBytes = 16 * 16 * codec.ColorDepth / 8 + codec.StorageSize / 8 + 1;
        var source = new MemoryDataSource("test", _offsetBytes + _elementsX * _elementsY * maxElementBytes + 16);
        source.Write(BitAddress.Zero, TestImageGenerator.RandomBytes((int)source.Length, 51));

        var arranger = new SequentialArranger(_elementsX, _elementsY, source, palette, _fixture.CodecFactory, codec);
        arranger.ChangePalette(palette);
        arranger.Move(new BitAddress(_offsetBytes * 8));

        Assert.Equal(_offsetBytes * 8, arranger.Address.Offset);
        AssertRoundTrips(arranger, source, 52);

        if (codec.CanResize)
        {
            arranger.ChangeCodec(_fixture.CodecFactory.CreateCodec(codecName, new Size(16, 16))!);
            arranger.ChangePalette(palette);

            Assert.Equal(new Size(16, 16), arranger.ElementPixelSize);
            Assert.Equal(_offsetBytes * 8, arranger.Address.Offset);
            AssertRoundTrips(arranger, source, 53);
        }
    }

    private static void AssertRoundTrips(SequentialArranger arranger, DataSource source, uint seed)
    {
        var before = CodecTestHelpers.ReadAll(source);

        new IndexedImage(arranger).SaveImage();
        Assert.Equal(before, CodecTestHelpers.ReadAll(source));

        var size = arranger.ArrangerPixelSize;
        var depth = arranger.ActiveCodec.ColorDepth;
        var indices = TestImageGenerator.Flatten(TestImageGenerator.RandomIndices(size.Width, size.Height, depth, seed));
        CodecTestHelpers.SaveIndices(arranger, indices);

        IndexedImageAssert.AreEqual(indices, new IndexedImage(arranger).Image, size.Width);
        BitAssert.EqualOutside(before, CodecTestHelpers.ReadAll(source), arranger.Address.Offset, arranger.ArrangerBitSize);
    }
}
