using System;
using ImageMagitek.Plugins;

namespace ImageMagitek.PluginSamples;

/// <summary>
/// C# implementation of the XML codec "PSX 4bpp Flow" (_codecs/PSX4bpp.xml).
/// </summary>
public sealed class Psx4BppCodec : IIndexedCodecPlugin
{
    public CodecInfo Info { get; } = new()
    {
        Name = "PSX 4bpp Plugin",
        Layout = CodecLayout.Single,
        ColorDepth = 4,
        DefaultWidth = 64,
        DefaultHeight = 64,
        WidthResizeIncrement = 2,
        HeightResizeIncrement = 1,
        CanEncode = true
    };

    public int GetStorageBits(int width, int height) => width * height * 4;

    public void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height)
    {
        // Each byte holds two pixels, with the left pixel in the low nibble
        for (int i = 0; i < width * height; i += 2)
        {
            pixels[i] = (byte)(encoded[i / 2] & 0xF);
            pixels[i + 1] = (byte)(encoded[i / 2] >> 4);
        }
    }

    public void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height)
    {
        for (int i = 0; i < width * height; i += 2)
            encoded[i / 2] = (byte)(pixels[i] | (pixels[i + 1] << 4));
    }
}
