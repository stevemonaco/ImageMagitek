using System;
using System.Runtime.InteropServices;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Plugins;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ImageTests;

public class SaveImageTests
{
    private static readonly CodecInfo _info = new()
    {
        Name = "Copy",
        Layout = CodecLayout.Tiled,
        ColorDepth = 8,
        DefaultWidth = 2,
        DefaultHeight = 2,
        CanEncode = true
    };

    [Fact]
    public void IndexedSaveImage_SecondEncodeThrows_LeavesSourceUnchanged()
    {
        var palette = TestImageGenerator.CreateDistinctPalette(8);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1,
            (x, _) => new IndexedCodecPluginAdapter(x == 0 ? new CopyIndexedPlugin() : new ThrowingIndexedPlugin(), palette));
        var source = arranger.GetElement(0, 0)!.Value.Source;
        source.Write(BitAddress.Zero, TestImageGenerator.RandomBytes((int)source.Length, 31));
        var before = CodecTestHelpers.ReadAll(source);

        var image = new IndexedImage(arranger);
        TestImageGenerator.Flatten(TestImageGenerator.RandomIndices(image.Width, image.Height, 8, 32)).CopyTo(image.Image, 0);

        Assert.Throws<InvalidOperationException>(image.SaveImage);
        Assert.Equal(before, CodecTestHelpers.ReadAll(source));
    }

    [Fact]
    public void DirectSaveImage_SecondEncodeThrows_LeavesSourceUnchanged()
    {
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Direct, 2, 1,
            (x, _) => new DirectCodecPluginAdapter(x == 0 ? new CopyDirectPlugin() : new ThrowingDirectPlugin()));
        var source = arranger.GetElement(0, 0)!.Value.Source;
        source.Write(BitAddress.Zero, TestImageGenerator.RandomBytes((int)source.Length, 33));
        var before = CodecTestHelpers.ReadAll(source);

        var image = new DirectImage(arranger);
        TestImageGenerator.Flatten(TestImageGenerator.RandomColors(image.Width, image.Height, 34)).CopyTo(image.Image, 0);

        Assert.Throws<InvalidOperationException>(image.SaveImage);
        Assert.Equal(before, CodecTestHelpers.ReadAll(source));
    }

    private class CopyIndexedPlugin : IIndexedCodecPlugin
    {
        public CodecInfo Info => _info;
        public int GetStorageBits(int width, int height) => 8 * width * height;
        public void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height) => encoded.CopyTo(pixels);
        public virtual void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height) => pixels.CopyTo(encoded);
    }

    private sealed class ThrowingIndexedPlugin : CopyIndexedPlugin
    {
        public override void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height) =>
            throw new FormatException("Encode failure");
    }

    private class CopyDirectPlugin : IDirectCodecPlugin
    {
        public CodecInfo Info => _info;
        public int GetStorageBits(int width, int height) => 32 * width * height;

        public void Decode(ReadOnlySpan<byte> encoded, Span<PluginColor> pixels, int width, int height) =>
            encoded.CopyTo(MemoryMarshal.AsBytes(pixels));

        public virtual void Encode(ReadOnlySpan<PluginColor> pixels, Span<byte> encoded, int width, int height) =>
            MemoryMarshal.AsBytes(pixels).CopyTo(encoded);
    }

    private sealed class ThrowingDirectPlugin : CopyDirectPlugin
    {
        public override void Encode(ReadOnlySpan<PluginColor> pixels, Span<byte> encoded, int width, int height) =>
            throw new FormatException("Encode failure");
    }
}
