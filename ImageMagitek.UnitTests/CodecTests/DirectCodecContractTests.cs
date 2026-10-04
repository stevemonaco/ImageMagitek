using System;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Contract every built-in direct-color codec must satisfy, independent of arrangers and data sources.
/// </summary>
[Collection("Codec")]
public partial class DirectCodecContractTests
{
    private readonly CodecFixture _fixture;

    public DirectCodecContractTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void CreatedCodec_HasRequestedSize(string codecName, int width, int height) =>
        AssertRequestedSize(codecName, width, height);

    [Theory(Skip = SizeIgnoredBug)]
    [MemberData(nameof(SizeIgnoredCases))]
    public void CreatedCodec_HasRequestedSize_SizeIgnoredBug(string codecName, int width, int height) =>
        AssertRequestedSize(codecName, width, height);

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void PixelsToBytesToPixels_RoundTrips(string codecName, int width, int height)
    {
        var codec = Create(codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var pixels = RepresentableColors(codecName, width, height, 1);

        var encoded = CodecTestHelpers.Encode(codec, el, pixels);
        var decoded = CodecTestHelpers.Decode(codec, el, encoded);

        ColorImageAssert.AreEqual(pixels, decoded);
    }

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void BytesToPixelsToBytes_RoundTrips(string codecName, int width, int height)
    {
        var codec = Create(codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var data = TestImageGenerator.RandomBytes((codec.StorageSize + 7) / 8, 2);

        var decoded = CodecTestHelpers.Decode(codec, el, data);
        var encoded = CodecTestHelpers.Encode(codec, el, decoded);

        Assert.Equal(CodecTestHelpers.MaskToStorageSize(data, codec.StorageSize), CodecTestHelpers.MaskToStorageSize(encoded, codec.StorageSize));
    }

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void Decode_IsIndependentOfPriorDecode(string codecName, int width, int height)
    {
        var codec = Create(codecName, width, height);
        var freshCodec = Create(codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var length = (codec.StorageSize + 7) / 8;
        var a = TestImageGenerator.RandomBytes(length, 11);
        var b = TestImageGenerator.RandomBytes(length, 12);

        var expected = CodecTestHelpers.Decode(freshCodec, el, a);
        var first = CodecTestHelpers.Decode(codec, el, a);
        CodecTestHelpers.Decode(codec, el, b);
        var second = CodecTestHelpers.Decode(codec, el, a);

        ColorImageAssert.AreEqual(expected, first);
        ColorImageAssert.AreEqual(expected, second);
    }

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void Encode_IsIndependentOfPriorEncode(string codecName, int width, int height)
    {
        var codec = Create(codecName, width, height);
        var freshCodec = Create(codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var a = TestImageGenerator.RandomColors(width, height, 21);
        var b = TestImageGenerator.RandomColors(width, height, 22);

        var expected = CodecTestHelpers.Encode(freshCodec, el, a);
        var first = CodecTestHelpers.Encode(codec, el, a);
        CodecTestHelpers.Encode(codec, el, b);
        var second = CodecTestHelpers.Encode(codec, el, a);

        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
    }

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void EncodeAfterDecode_IsIndependent(string codecName, int width, int height)
    {
        var codec = Create(codecName, width, height);
        var freshCodec = Create(codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var pixels = TestImageGenerator.RandomColors(width, height, 23);
        var data = TestImageGenerator.RandomBytes((codec.StorageSize + 7) / 8, 24);

        var expected = CodecTestHelpers.Encode(freshCodec, el, pixels);
        CodecTestHelpers.Decode(codec, el, data);
        var actual = CodecTestHelpers.Encode(codec, el, pixels);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void Decode_TooShortBuffer_ThrowsArgumentException(string codecName, int width, int height)
    {
        var codec = Create(codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var buffer = new byte[(codec.StorageSize + 7) / 8 - 1];

        Assert.Throws<ArgumentException>(() => codec.DecodeElement(el, buffer));
    }

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void Encode_WrongSizeImage_ThrowsArgumentException(string codecName, int width, int height)
    {
        var codec = Create(codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);

        Assert.Throws<ArgumentException>(() => { codec.EncodeElement(el, new ColorRgba32[height + 1, width]); });
        Assert.Throws<ArgumentException>(() => { codec.EncodeElement(el, new ColorRgba32[height, width + 1]); });
    }

    private IDirectCodec Create(string codecName, int width, int height) =>
        CodecTestHelpers.CreateDirectCodec(_fixture.CodecFactory, codecName, width, height);

    private void AssertRequestedSize(string codecName, int width, int height)
    {
        var codec = Create(codecName, width, height);

        Assert.Equal((width, height), (codec.Width, codec.Height));
        Assert.Equal((width, height), (codec.NativeBuffer.GetLength(1), codec.NativeBuffer.GetLength(0)));
    }
}