using System;
using ImageMagitek.Plugins;

namespace ImageMagitek.PluginSamples;

/// <summary>
/// C# implementation of the XML codec "SNES 3bpp Flow" (_codecs/SNES3bpp Flow.xml).
/// </summary>
public sealed class Snes3BppCodec : IIndexedCodecPlugin
{
    public CodecInfo Info { get; } = new()
    {
        Name = "SNES 3bpp Plugin",
        Layout = CodecLayout.Tiled,
        ColorDepth = 3,
        DefaultWidth = 8,
        DefaultHeight = 8,
        WidthResizeIncrement = 1,
        HeightResizeIncrement = 1,
        CanEncode = true
    };

    public int GetStorageBits(int width, int height) => 3 * width * height;

    public void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height)
    {
        // Planes 1 and 2 alternate by row, then plane 3 follows as a block. Bits are numbered MSB-first.
        var offsetPlane3 = width * height * 2;

        for (int y = 0; y < height; y++)
        {
            var offsetPlane1 = y * width * 2;
            var offsetPlane2 = offsetPlane1 + width;

            for (int x = 0; x < width; x++)
            {
                var bp1 = SampleBits.ReadBit(encoded, offsetPlane1 + x);
                var bp2 = SampleBits.ReadBit(encoded, offsetPlane2 + x);
                var bp3 = SampleBits.ReadBit(encoded, offsetPlane3++);

                pixels[y * width + x] = (byte)(bp1 | (bp2 << 1) | (bp3 << 2));
            }
        }
    }

    public void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height)
    {
        var offsetPlane3 = width * height * 2;

        for (int y = 0; y < height; y++)
        {
            var offsetPlane1 = y * width * 2;
            var offsetPlane2 = offsetPlane1 + width;

            for (int x = 0; x < width; x++)
            {
                var index = pixels[y * width + x];

                SampleBits.WriteBit(encoded, offsetPlane1 + x, index & 1);
                SampleBits.WriteBit(encoded, offsetPlane2 + x, (index >> 1) & 1);
                SampleBits.WriteBit(encoded, offsetPlane3++, (index >> 2) & 1);
            }
        }
    }
}
