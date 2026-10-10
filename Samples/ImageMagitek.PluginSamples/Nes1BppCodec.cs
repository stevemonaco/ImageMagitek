using System;
using ImageMagitek.Plugins;

namespace ImageMagitek.PluginSamples;

/// <summary>
/// C# implementation of the XML codec "NES 1bpp" (_codecs/NES1bpp.xml).
/// </summary>
public sealed class Nes1BppCodec : IIndexedCodecPlugin
{
    public CodecInfo Info { get; } = new()
    {
        Name = "NES 1bpp Plugin",
        Layout = CodecLayout.Tiled,
        ColorDepth = 1,
        DefaultWidth = 8,
        DefaultHeight = 8,
        WidthResizeIncrement = 1,
        HeightResizeIncrement = 1,
        CanEncode = true
    };

    public int GetStorageBits(int width, int height) => width * height;

    public void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height)
    {
        // One bit per pixel in row-major order, MSB-first
        for (int i = 0; i < width * height; i++)
            pixels[i] = (byte)SampleBits.ReadBit(encoded, i);
    }

    public void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height)
    {
        for (int i = 0; i < width * height; i++)
            SampleBits.WriteBit(encoded, i, pixels[i] & 1);
    }
}
