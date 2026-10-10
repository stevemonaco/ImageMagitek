using System;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Plugins;
using Xunit;

namespace ImageMagitek.UnitTests;

public class CodecPluginAdapterTests
{
    [Fact]
    public void PluginColor_HasColorRgba32Layout()
    {
        Span<PluginColor> pluginColors = [new PluginColor(1, 2, 3, 4)];
        Span<ColorRgba32> hostColors = [new ColorRgba32(1, 2, 3, 4)];

        Assert.Equal(Unsafe.SizeOf<ColorRgba32>(), Unsafe.SizeOf<PluginColor>());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, MemoryMarshal.AsBytes(pluginColors).ToArray());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, MemoryMarshal.AsBytes(hostColors).ToArray());
    }

    [Fact]
    public void Create_FixedSizePlugin_UsesDefaultSize()
    {
        var codec = new IndexedCodecPluginAdapter(new FixedSizePlugin(), CreatePalette(), 16, 16);

        Assert.Equal((12, 6), (codec.Width, codec.Height));
        Assert.False(codec.CanResize);
        Assert.Equal(12 * 6, codec.StorageSize);
    }

    [Fact]
    public void Create_OneResizableDimension_RoundsOnlyThatDimension()
    {
        var codec = new IndexedCodecPluginAdapter(new WidthOnlyPlugin(), CreatePalette(), 18, 18);
        var small = new IndexedCodecPluginAdapter(new WidthOnlyPlugin(), CreatePalette(), 1, 1);

        Assert.Equal((16, 6), (codec.Width, codec.Height));
        Assert.Equal((4, 6), (small.Width, small.Height));
        Assert.True(codec.CanResize);
    }

    [Theory]
    [InlineData(typeof(BlankNamePlugin), "Name")]
    [InlineData(typeof(ZeroDepthPlugin), "ColorDepth")]
    [InlineData(typeof(DeepIndexedPlugin), "ColorDepth")]
    [InlineData(typeof(DeepDirectPlugin), "ColorDepth")]
    [InlineData(typeof(ZeroWidthPlugin), "DefaultWidth")]
    [InlineData(typeof(ZeroHeightPlugin), "DefaultHeight")]
    [InlineData(typeof(NegativeWidthIncrementPlugin), "WidthResizeIncrement")]
    [InlineData(typeof(NegativeHeightIncrementPlugin), "HeightResizeIncrement")]
    [InlineData(typeof(ZeroStoragePlugin), "GetStorageBits")]
    public void AddCodecPlugin_InvalidInfo_FailsNamingField(Type pluginType, string field)
    {
        var factory = CreateFactory();

        var result = factory.AddCodecPlugin(pluginType);

        Assert.True(result.HasFailed);
        Assert.Contains(pluginType.Name, result.AsError.Reason);
        Assert.Contains(field, result.AsError.Reason);
        Assert.DoesNotContain("Fake", factory.GetRegisteredCodecNames());
    }

    [Theory]
    [InlineData(typeof(FakePluginBase), "abstract")]
    [InlineData(typeof(NoParameterlessConstructorPlugin), "parameterless")]
    [InlineData(typeof(BothKindsPlugin), "exactly one")]
    [InlineData(typeof(NeitherKindPlugin), "exactly one")]
    [InlineData(typeof(ThrowingInfoPlugin), "Info failure")]
    [InlineData(typeof(ThrowingConstructorPlugin), "Constructor failure")]
    public void AddCodecPlugin_InvalidType_FailsWithReason(Type pluginType, string reason)
    {
        var factory = CreateFactory();

        var result = factory.AddCodecPlugin(pluginType);

        Assert.True(result.HasFailed);
        Assert.Contains(pluginType.Name, result.AsError.Reason);
        Assert.Contains(reason, result.AsError.Reason);
    }

    [Fact]
    public void AddCodecPlugin_NameCollision_KeepsFirstRegistration()
    {
        var factory = CreateFactory();

        Assert.Equal("Collide", factory.AddCodecPlugin(typeof(CollideFirstPlugin)).AsSuccess.Result);
        var result = factory.AddCodecPlugin(typeof(CollideSecondPlugin));

        Assert.True(result.HasFailed);
        Assert.Contains(nameof(CollideSecondPlugin), result.AsError.Reason);
        Assert.Contains(nameof(CollideFirstPlugin), result.AsError.Reason);
        Assert.Equal(1, factory.CreateCodec("Collide")!.ColorDepth);
    }

    [Fact]
    public void CreateCodec_ReturnsNewAdapterWithDefaultPalette()
    {
        var factory = CreateFactory();
        factory.AddCodecPlugin(typeof(CollideFirstPlugin));

        var first = Assert.IsType<IndexedCodecPluginAdapter>(factory.CreateCodec("Collide", new Size(16, 8)));
        var second = Assert.IsType<IndexedCodecPluginAdapter>(factory.CreateCodec("Collide"));

        Assert.NotSame(first, second);
        Assert.Same(factory.DefaultPalette, first.Palette);
        Assert.Equal((16, 8), (first.Width, first.Height));
        Assert.Equal((8, 8), (second.Width, second.Height));
    }

    [Fact]
    public void DirectPlugin_DecodesThroughReinterpretedPixels()
    {
        var factory = CreateFactory();
        factory.AddCodecPlugin(typeof(CopyDirectPlugin));
        var codec = Assert.IsType<DirectCodecPluginAdapter>(factory.CreateCodec("Copy Direct", new Size(2, 1)));
        var el = CodecTestHelpers.CreateElement(codec);
        byte[] encoded = [1, 2, 3, 4, 5, 6, 7, 8];

        var decoded = CodecTestHelpers.Decode(codec, el, encoded);

        Assert.Equal(new ColorRgba32(1, 2, 3, 4), decoded[0, 0]);
        Assert.Equal(new ColorRgba32(5, 6, 7, 8), decoded[0, 1]);
        Assert.Equal(encoded, CodecTestHelpers.Encode(codec, el, decoded));
    }

    [Fact]
    public void EncodeElement_CanEncodeFalse_ThrowsWithoutCallingPlugin()
    {
        var plugin = new DecodeOnlyPlugin();
        var codec = new IndexedCodecPluginAdapter(plugin, CreatePalette());
        var el = CodecTestHelpers.CreateElement(codec);

        Assert.Throws<NotSupportedException>(() => { codec.EncodeElement(el, new byte[codec.Height, codec.Width]); });
        Assert.Equal(0, plugin.EncodeCalls);
    }

    [Fact]
    public void SaveImage_PluginWritesPastStorageBits_LeavesNeighborBitsUnchanged()
    {
        const int offsetBits = 5;
        var codec = new IndexedCodecPluginAdapter(new OverwritingPlugin(), CreatePalette(), 3, 3);
        var before = new byte[8];
        var source = new MemoryDataSource("test", before.Length);
        source.Write(BitAddress.Zero, before);

        var arranger = new ScatteredArranger("test", PixelColorType.Indexed, ElementLayout.Tiled, 1, 1, codec.Width, codec.Height);
        arranger.SetElement(new ArrangerElement(0, 0, source, new BitAddress(offsetBits), codec), 0, 0);
        new IndexedImage(arranger).SaveImage();

        var after = CodecTestHelpers.ReadAll(source);
        Assert.Equal(27, codec.StorageSize);
        BitAssert.EqualOutside(before, after, offsetBits, codec.StorageSize);
        Assert.Equal(0xFF >> offsetBits, after[0]);
    }

    [Fact]
    public void DecodeElement_PluginThrows_ReturnsZerosAndRaisesDecodeFailedOnce()
    {
        var codec = new IndexedCodecPluginAdapter(new ThrowingDecodePlugin(), CreatePalette());
        var el = CodecTestHelpers.CreateElement(codec);
        var failures = 0;
        void OnDecodeFailed(string name, Exception ex)
        {
            if (name == ThrowingDecodePlugin.Name)
                failures++;
        }

        CodecPluginEvents.DecodeFailed += OnDecodeFailed;
        try
        {
            var first = CodecTestHelpers.Decode(codec, el, new byte[8]);
            var second = CodecTestHelpers.Decode(codec, el, new byte[8]);

            Assert.All(first.Cast<byte>().Concat(second.Cast<byte>()), x => Assert.Equal(0, x));
            Assert.Equal(1, failures);
        }
        finally
        {
            CodecPluginEvents.DecodeFailed -= OnDecodeFailed;
        }
    }

    [Fact]
    public void EncodeElement_PluginThrows_ThrowsNamingCodec()
    {
        var codec = new IndexedCodecPluginAdapter(new ThrowingEncodePlugin(), CreatePalette());
        var el = CodecTestHelpers.CreateElement(codec);

        var ex = Assert.Throws<InvalidOperationException>(() => { codec.EncodeElement(el, new byte[codec.Height, codec.Width]); });

        Assert.Contains("Throwing Encode", ex.Message);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    private static CodecFactory CreateFactory() => new(CreatePalette(), []);

    private static Palette CreatePalette() => TestImageGenerator.CreateDistinctPalette(8);

    private static CodecInfo MakeInfo(string name = "Fake", int depth = 1, int defaultWidth = 8, int defaultHeight = 8,
        int widthIncrement = 1, int heightIncrement = 1, bool canEncode = true) => new()
    {
        Name = name,
        Layout = CodecLayout.Tiled,
        ColorDepth = depth,
        DefaultWidth = defaultWidth,
        DefaultHeight = defaultHeight,
        WidthResizeIncrement = widthIncrement,
        HeightResizeIncrement = heightIncrement,
        CanEncode = canEncode
    };

    private abstract class FakePluginBase : IIndexedCodecPlugin
    {
        public virtual CodecInfo Info => MakeInfo();
        public virtual int GetStorageBits(int width, int height) => Info.ColorDepth * width * height;
        public virtual void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height) { }
        public virtual void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height) { }
    }

    private sealed class FixedSizePlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(defaultWidth: 12, defaultHeight: 6, widthIncrement: 0, heightIncrement: 0);
    }

    private sealed class WidthOnlyPlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(defaultWidth: 12, defaultHeight: 6, widthIncrement: 4, heightIncrement: 0);
    }

    private sealed class BlankNamePlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(name: " ");
    }

    private sealed class ZeroDepthPlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(depth: 0);
        public override int GetStorageBits(int width, int height) => width * height;
    }

    private sealed class DeepIndexedPlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(depth: 9);
    }

    private sealed class DeepDirectPlugin : IDirectCodecPlugin
    {
        public CodecInfo Info => MakeInfo(depth: 33);
        public int GetStorageBits(int width, int height) => 32 * width * height;
        public void Decode(ReadOnlySpan<byte> encoded, Span<PluginColor> pixels, int width, int height) { }
        public void Encode(ReadOnlySpan<PluginColor> pixels, Span<byte> encoded, int width, int height) { }
    }

    private sealed class ZeroWidthPlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(defaultWidth: 0);
    }

    private sealed class ZeroHeightPlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(defaultHeight: 0);
    }

    private sealed class NegativeWidthIncrementPlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(widthIncrement: -1);
    }

    private sealed class NegativeHeightIncrementPlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(heightIncrement: -1);
    }

    private sealed class ZeroStoragePlugin : FakePluginBase
    {
        public override int GetStorageBits(int width, int height) => 0;
    }

    private sealed class NoParameterlessConstructorPlugin(int depth) : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(depth: depth);
    }

    private sealed class BothKindsPlugin : FakePluginBase, IDirectCodecPlugin
    {
        public void Decode(ReadOnlySpan<byte> encoded, Span<PluginColor> pixels, int width, int height) { }
        public void Encode(ReadOnlySpan<PluginColor> pixels, Span<byte> encoded, int width, int height) { }
    }

    private sealed class NeitherKindPlugin : ICodecPlugin
    {
        public CodecInfo Info => MakeInfo();
        public int GetStorageBits(int width, int height) => width * height;
    }

    private sealed class ThrowingInfoPlugin : FakePluginBase
    {
        public override CodecInfo Info => throw new InvalidOperationException("Info failure");
    }

    private sealed class ThrowingConstructorPlugin : FakePluginBase
    {
        public ThrowingConstructorPlugin() => throw new InvalidOperationException("Constructor failure");
    }

    private sealed class CollideFirstPlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(name: "Collide", depth: 1);
    }

    private sealed class CollideSecondPlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(name: "Collide", depth: 2);
    }

    private sealed class CopyDirectPlugin : IDirectCodecPlugin
    {
        public CodecInfo Info => MakeInfo(name: "Copy Direct", depth: 32);
        public int GetStorageBits(int width, int height) => 32 * width * height;

        public void Decode(ReadOnlySpan<byte> encoded, Span<PluginColor> pixels, int width, int height) =>
            encoded.CopyTo(MemoryMarshal.AsBytes(pixels));

        public void Encode(ReadOnlySpan<PluginColor> pixels, Span<byte> encoded, int width, int height) =>
            MemoryMarshal.AsBytes(pixels).CopyTo(encoded);
    }

    private sealed class DecodeOnlyPlugin : FakePluginBase
    {
        public int EncodeCalls { get; private set; }

        public override CodecInfo Info => MakeInfo(canEncode: false);

        public override void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height) => EncodeCalls++;
    }

    private sealed class OverwritingPlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(depth: 3);

        public override void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height) => encoded.Fill(0xFF);
    }

    private sealed class ThrowingDecodePlugin : FakePluginBase
    {
        public const string Name = "Throwing Decode";

        public override CodecInfo Info => MakeInfo(name: Name);

        public override void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height)
        {
            pixels.Fill(1);
            throw new FormatException("Decode failure");
        }
    }

    private sealed class ThrowingEncodePlugin : FakePluginBase
    {
        public override CodecInfo Info => MakeInfo(name: "Throwing Encode");

        public override void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height) =>
            throw new FormatException("Encode failure");
    }
}
