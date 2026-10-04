using System.Linq;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Saving one element must change only the bits in [address, address + StorageSize) and leave neighboring data intact.
/// </summary>
[Collection("Codec")]
public class ElementIsolationTests
{
    private const int _alignedOffsetBits = 5 * 8;
    private const int _unalignedOffsetBits = 5 * 8 + 3;
    private const string UnalignedAddressBug =
        "DataSource.Read/Write with a non-byte-aligned BitAddress do not shift data (ReadUnshifted/WriteUnshifted); see CodecRework findings";

    private readonly CodecFixture _fixture;

    public ElementIsolationTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string, int, int, bool> IsolationCases
    {
        get
        {
            var fixture = CodecFixture.Shared;
            var data = new TheoryData<string, int, int, bool>();

            foreach (var name in fixture.XmlCodecNames)
            {
                var codec = fixture.CodecFactory.CreateCodec(name)!;
                data.Add(name, codec.Width, codec.Height, false);
                data.Add(name, codec.Width, codec.Height, true);
            }

            data.Add("NES 1bpp", 3, 3, false);
            data.Add("NES 1bpp", 3, 3, true);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(IsolationCases))]
    public void SaveElement_ByteAligned_ChangesOnlyElementBits(string codecName, int width, int height, bool randomSentinel) =>
        AssertIsolated(codecName, width, height, randomSentinel, _alignedOffsetBits);

    [Theory(Skip = UnalignedAddressBug)]
    [MemberData(nameof(IsolationCases))]
    public void SaveElement_NotByteAligned_ChangesOnlyElementBits(string codecName, int width, int height, bool randomSentinel) =>
        AssertIsolated(codecName, width, height, randomSentinel, _unalignedOffsetBits);

    private void AssertIsolated(string codecName, int width, int height, bool randomSentinel, int offsetBits)
    {
        var palette = TestImageGenerator.CreateDistinctPalette(8);
        var codec = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height, palette);
        var length = (offsetBits + codec.StorageSize + 7) / 8 + 8;
        var before = randomSentinel ? TestImageGenerator.RandomBytes(length, 61) : Enumerable.Repeat((byte)0xFF, length).ToArray();

        var source = new MemoryDataSource("test", length);
        source.Write(BitAddress.Zero, before);

        var arranger = new ScatteredArranger("test", PixelColorType.Indexed, ElementLayout.Tiled, 1, 1, width, height);
        arranger.SetElement(new ArrangerElement(0, 0, source, new BitAddress(offsetBits), codec), 0, 0);

        var indices = TestImageGenerator.Flatten(TestImageGenerator.RandomIndices(width, height, codec.ColorDepth, 62));
        CodecTestHelpers.SaveIndices(arranger, indices);

        var after = source.Read(BitAddress.Zero, length * 8);
        BitAssert.EqualOutside(before, after, offsetBits, codec.StorageSize);
        IndexedImageAssert.AreEqual(indices, new IndexedImage(arranger).Image, width);
    }

}
