using System;
using ImageMagitek.Plugins;

namespace ImageMagitek.PluginSamples;

public sealed class Snes4BppCodec : IIndexedCodecPlugin
{
    public CodecInfo Info { get; } = new()
    {
        Name = "SNES 4bpp Plugin",
        Layout = CodecLayout.Tiled,
        ColorDepth = 4,
        DefaultWidth = 8,
        DefaultHeight = 8,
        WidthResizeIncrement = 1,
        HeightResizeIncrement = 1,
        CanEncode = true
    };

    public int GetStorageBits(int width, int height) => 4 * width * height;

    public void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height)
    {
        // Planes 1 and 2 alternate by row, then planes 3 and 4 do the same. Bits are numbered MSB-first.
        var pairSize = width * height * 2;

        for (int y = 0; y < height; y++)
        {
            var offsetPlane1 = y * width * 2;
            var offsetPlane2 = offsetPlane1 + width;
            var offsetPlane3 = offsetPlane1 + pairSize;
            var offsetPlane4 = offsetPlane2 + pairSize;

            for (int x = 0; x < width; x++)
            {
                var bp1 = SampleBits.ReadBit(encoded, offsetPlane1 + x);
                var bp2 = SampleBits.ReadBit(encoded, offsetPlane2 + x);
                var bp3 = SampleBits.ReadBit(encoded, offsetPlane3 + x);
                var bp4 = SampleBits.ReadBit(encoded, offsetPlane4 + x);

                pixels[y * width + x] = (byte)(bp1 | (bp2 << 1) | (bp3 << 2) | (bp4 << 3));
            }
        }
    }

    public void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height)
    {
        var pairSize = width * height * 2;

        for (int y = 0; y < height; y++)
        {
            var offsetPlane1 = y * width * 2;
            var offsetPlane2 = offsetPlane1 + width;
            var offsetPlane3 = offsetPlane1 + pairSize;
            var offsetPlane4 = offsetPlane2 + pairSize;

            for (int x = 0; x < width; x++)
            {
                var index = pixels[y * width + x];

                SampleBits.WriteBit(encoded, offsetPlane1 + x, index & 1);
                SampleBits.WriteBit(encoded, offsetPlane2 + x, (index >> 1) & 1);
                SampleBits.WriteBit(encoded, offsetPlane3 + x, (index >> 2) & 1);
                SampleBits.WriteBit(encoded, offsetPlane4 + x, (index >> 3) & 1);
            }
        }
    }
}
