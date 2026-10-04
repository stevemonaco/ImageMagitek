using System;
using System.Collections.Generic;
using System.Reflection;
using ImageMagitek.Codec;
using Xunit;
using Xunit.Sdk;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Contract every indexed codec must satisfy, independent of where it is defined.
/// Derived classes override <see cref="CreateCodec"/> and declare a public static <c>ContractCases</c> property of (codec name, width, height) cases.
/// Codecs with <see cref="IGraphicsCodec.CanEncode"/> false must reject encoding with <see cref="NotSupportedException"/>, and only their decode checks run.
/// </summary>
public abstract class IndexedCodecContract
{
    private const int _alignedOffsetBits = 5 * 8;

    protected abstract IIndexedCodec CreateCodec(string codecName, int width, int height);

    [Theory]
    [ContractCases]
    public void PixelsToBytesToPixels_RoundTrips(string codecName, int width, int height)
    {
        var codec = CreateCodec(codecName, width, height);
        if (!CanEncodeOrAssertRejected(codec))
            return;

        var el = CodecTestHelpers.CreateElement(codec);
        var pixels = TestImageGenerator.RandomIndices(width, height, codec.ColorDepth, 1);

        var encoded = CodecTestHelpers.Encode(codec, el, pixels);
        var decoded = CodecTestHelpers.Decode(codec, el, encoded);

        IndexedImageAssert.AreEqual(pixels, decoded);
    }

    [Theory]
    [ContractCases]
    public void BytesToPixelsToBytes_RoundTrips(string codecName, int width, int height)
    {
        var codec = CreateCodec(codecName, width, height);
        if (!CanEncodeOrAssertRejected(codec))
            return;

        var el = CodecTestHelpers.CreateElement(codec);
        var data = TestImageGenerator.RandomBytes((codec.StorageSize + 7) / 8, 2);

        var decoded = CodecTestHelpers.Decode(codec, el, data);
        var encoded = CodecTestHelpers.Encode(codec, el, decoded);

        Assert.Equal(CodecTestHelpers.MaskToStorageSize(data, codec.StorageSize), CodecTestHelpers.MaskToStorageSize(encoded, codec.StorageSize));
    }

    [Theory]
    [ContractCases]
    public void Decode_IsIndependentOfPriorDecode(string codecName, int width, int height)
    {
        var codec = CreateCodec(codecName, width, height);
        var freshCodec = CreateCodec(codecName, width, height);
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
    [ContractCases]
    public void Encode_IsIndependentOfPriorEncode(string codecName, int width, int height)
    {
        var codec = CreateCodec(codecName, width, height);
        if (!CanEncodeOrAssertRejected(codec))
            return;

        var freshCodec = CreateCodec(codecName, width, height);
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
    [ContractCases]
    public void EncodeAfterDecode_IsIndependent(string codecName, int width, int height)
    {
        var codec = CreateCodec(codecName, width, height);
        if (!CanEncodeOrAssertRejected(codec))
            return;

        var freshCodec = CreateCodec(codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var pixels = TestImageGenerator.RandomIndices(width, height, codec.ColorDepth, 23);
        var data = TestImageGenerator.RandomBytes((codec.StorageSize + 7) / 8, 24);

        var expected = CodecTestHelpers.Encode(freshCodec, el, pixels);
        CodecTestHelpers.Decode(codec, el, data);
        var actual = CodecTestHelpers.Encode(codec, el, pixels);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [ContractCases]
    public void Decode_TooShortBuffer_ThrowsArgumentException(string codecName, int width, int height)
    {
        var codec = CreateCodec(codecName, width, height);
        var el = CodecTestHelpers.CreateElement(codec);
        var buffer = new byte[(codec.StorageSize + 7) / 8 - 1];

        Assert.Throws<ArgumentException>(() => codec.DecodeElement(el, buffer));
    }

    [Theory]
    [ContractCases]
    public void Encode_WrongSizeImage_ThrowsArgumentException(string codecName, int width, int height)
    {
        var codec = CreateCodec(codecName, width, height);
        if (!CanEncodeOrAssertRejected(codec))
            return;

        var el = CodecTestHelpers.CreateElement(codec);

        Assert.Throws<ArgumentException>(() => { codec.EncodeElement(el, new byte[height + 1, width]); });
        Assert.Throws<ArgumentException>(() => { codec.EncodeElement(el, new byte[height, width + 1]); });
    }

    [Theory]
    [ContractCases]
    public void ResizeIncrements_AreHonored(string codecName, int width, int height)
    {
        var codec = CreateCodec(codecName, width, height);

        if (!codec.CanResize)
        {
            Assert.Equal((codec.DefaultWidth, codec.DefaultHeight), (codec.GetPreferredWidth(width + 1), codec.GetPreferredHeight(height + 1)));
            return;
        }

        var widthIncrement = codec.WidthResizeIncrement;
        var heightIncrement = codec.HeightResizeIncrement;
        Assert.True(widthIncrement > 0, $"{nameof(IGraphicsCodec.WidthResizeIncrement)} is {widthIncrement}");
        Assert.True(heightIncrement > 0, $"{nameof(IGraphicsCodec.HeightResizeIncrement)} is {heightIncrement}");

        foreach (var request in new[] { 1, width - 1, width, width + 1, 3 * width + 1 })
        {
            var preferred = codec.GetPreferredWidth(request);
            Assert.True(preferred > 0 && preferred % widthIncrement == 0, $"Preferred width {preferred} for {request} is not a positive multiple of {widthIncrement}");
        }

        foreach (var request in new[] { 1, height - 1, height, height + 1, 3 * height + 1 })
        {
            var preferred = codec.GetPreferredHeight(request);
            Assert.True(preferred > 0 && preferred % heightIncrement == 0, $"Preferred height {preferred} for {request} is not a positive multiple of {heightIncrement}");
        }

        var resized = CreateCodec(codecName, 2 * widthIncrement, 2 * heightIncrement);
        Assert.Equal((2 * widthIncrement, 2 * heightIncrement), (resized.Width, resized.Height));
    }

    [Theory]
    [ContractCases]
    public void SaveElement_ByteAligned_ChangesOnlyElementBits(string codecName, int width, int height)
    {
        var codec = CreateCodec(codecName, width, height);
        if (!CanEncodeOrAssertRejected(codec))
            return;

        CodecTestHelpers.AssertSaveIsolated(codec, randomSentinel: false, _alignedOffsetBits);
        CodecTestHelpers.AssertSaveIsolated(CreateCodec(codecName, width, height), randomSentinel: true, _alignedOffsetBits);
    }

    private static bool CanEncodeOrAssertRejected(IIndexedCodec codec)
    {
        if (codec.CanEncode)
            return true;

        var el = CodecTestHelpers.CreateElement(codec);
        Assert.Throws<NotSupportedException>(() => { codec.EncodeElement(el, new byte[codec.Height, codec.Width]); });
        return false;
    }
}

/// <summary>
/// Supplies theory data from the static <c>ContractCases</c> property of the class that runs the test.
/// <see cref="MemberDataAttribute"/> resolves members on the declaring type, which for inherited theories is the abstract base.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ContractCasesAttribute : DataAttribute
{
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var property = testMethod.ReflectedType!.GetProperty("ContractCases", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"'{testMethod.ReflectedType}' must declare a public static ContractCases property");

        return (IEnumerable<object[]>)property.GetValue(null)!;
    }
}