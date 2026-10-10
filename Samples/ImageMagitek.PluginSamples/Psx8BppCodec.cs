using System;
using ImageMagitek.Plugins;

namespace ImageMagitek.PluginSamples;

/// <summary>
/// C# implementation of the XML codec "PSX 8bpp Flow" (_codecs/PSX8bpp.xml).
/// </summary>
public sealed class Psx8BppCodec : IIndexedCodecPlugin
{
    public CodecInfo Info { get; } = new()
    {
        Name = "PSX 8bpp Plugin",
        Layout = CodecLayout.Single,
        ColorDepth = 8,
        DefaultWidth = 64,
        DefaultHeight = 64,
        WidthResizeIncrement = 1,
        HeightResizeIncrement = 1,
        CanEncode = true
    };

    public int GetStorageBits(int width, int height) => width * height * 8;

    public void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height) =>
        encoded[..(width * height)].CopyTo(pixels);

    public void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height) =>
        pixels.CopyTo(encoded);
}
