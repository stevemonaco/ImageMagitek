using System;
using System.IO;
using System.Xml.Linq;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Contract every shipped XML flow and pattern codec must satisfy, independent of arrangers and data sources.
/// </summary>
[Collection("Codec")]
public partial class IndexedCodecContractTests
{
    private readonly CodecFixture _fixture;

    public IndexedCodecContractTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void AllShippedXmlCodecsLoad()
    {
        var files = Directory.GetFiles(TestPaths.CodecsPath, "*.xml");

        Assert.Equal(files.Length, _fixture.XmlCodecNames.Count);

        foreach (var file in files)
        {
            var root = XDocument.Load(file).Root!;
            var name = root.Attribute("name")!.Value;
            var isFlow = root.Name.LocalName == "flowcodec";
            var expectedWidth = int.Parse(root.Element(isFlow ? "defaultwidth" : "width")!.Value);
            var expectedHeight = int.Parse(root.Element(isFlow ? "defaultheight" : "height")!.Value);

            var codec = _fixture.CodecFactory.CreateCodec(name)!;

            Assert.Contains(name, _fixture.XmlCodecNames);
            Assert.Equal((expectedWidth, expectedHeight), (codec.DefaultWidth, codec.DefaultHeight));
            Assert.Equal((expectedWidth, expectedHeight), (codec.Width, codec.Height));
        }
    }

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void PixelsToBytesToPixels_RoundTrips(string codecName, int width, int height) =>
        AssertPixelsRoundTrip(codecName, width, height);

    [Theory(Skip = CodecTestHelpers.RowInterlaceEncodeBug)]
    [MemberData(nameof(KnownBugCases))]
    public void PixelsToBytesToPixels_RowInterlacedNonSquare_RoundTrips(string codecName, int width, int height) =>
        AssertPixelsRoundTrip(codecName, width, height);

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void BytesToPixelsToBytes_RoundTrips(string codecName, int width, int height) =>
        AssertBytesRoundTrip(codecName, width, height);

    [Theory(Skip = CodecTestHelpers.RowInterlaceEncodeBug)]
    [MemberData(nameof(KnownBugCases))]
    public void BytesToPixelsToBytes_RowInterlacedNonSquare_RoundTrips(string codecName, int width, int height) =>
        AssertBytesRoundTrip(codecName, width, height);

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Decode_IsIndependentOfPriorDecode(string codecName, int width, int height)
    {
        var codec = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height);
        var freshCodec = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var length = (codec.StorageSize + 7) / 8;
        var a = TestImageGenerator.RandomBytes(length, 11);
        var b = TestImageGenerator.RandomBytes(length, 12);

        var expected = CodecTestHelpers.Decode(freshCodec, el, a);
        var first = CodecTestHelpers.Decode(codec, el, a);
        CodecTestHelpers.Decode(codec, el, b);
        var second = CodecTestHelpers.Decode(codec, el, a);

        IndexedImageAssert.AreEqual(expected, first);
        IndexedImageAssert.AreEqual(expected, second);
    }

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void Encode_IsIndependentOfPriorEncode(string codecName, int width, int height)
    {
        var codec = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height);
        var freshCodec = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var a = TestImageGenerator.RandomIndices(width, height, codec.ColorDepth, 21);
        var b = TestImageGenerator.RandomIndices(width, height, codec.ColorDepth, 22);

        var expected = CodecTestHelpers.Encode(freshCodec, el, a);
        var first = CodecTestHelpers.Encode(codec, el, a);
        CodecTestHelpers.Encode(codec, el, b);
        var second = CodecTestHelpers.Encode(codec, el, a);

        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Decode_TooShortBuffer_ThrowsArgumentException(string codecName, int width, int height)
    {
        var codec = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var buffer = new byte[(codec.StorageSize + 7) / 8 - 1];

        Assert.Throws<ArgumentException>(() => codec.DecodeElement(el, buffer));
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Encode_WrongSizeImage_ThrowsArgumentException(string codecName, int width, int height)
    {
        var codec = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);

        Assert.Throws<ArgumentException>(() => { codec.EncodeElement(el, new byte[height + 1, width]); });
        Assert.Throws<ArgumentException>(() => { codec.EncodeElement(el, new byte[height, width + 1]); });
    }

    private void AssertPixelsRoundTrip(string codecName, int width, int height)
    {
        var codec = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var pixels = TestImageGenerator.RandomIndices(width, height, codec.ColorDepth, 1);

        var encoded = CodecTestHelpers.Encode(codec, el, pixels);
        var decoded = CodecTestHelpers.Decode(codec, el, encoded);

        IndexedImageAssert.AreEqual(pixels, decoded);
    }

    private void AssertBytesRoundTrip(string codecName, int width, int height)
    {
        var codec = CodecTestHelpers.CreateCodec(_fixture.CodecFactory, codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var data = TestImageGenerator.RandomBytes((codec.StorageSize + 7) / 8, 2);

        var decoded = CodecTestHelpers.Decode(codec, el, data);
        var encoded = CodecTestHelpers.Encode(codec, el, decoded);

        Assert.Equal(CodecTestHelpers.MaskToStorageSize(data, codec.StorageSize), CodecTestHelpers.MaskToStorageSize(encoded, codec.StorageSize));
    }
}
