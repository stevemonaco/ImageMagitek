using System;
using ImageMagitek.Plugins;

namespace ImageMagitek.PluginSamples;

/// <summary>
/// In-game, this font is a 8px wide, variable height tile
/// The font location is 0x25005
/// This codec is view-only and does not support editing.
/// </summary>
public sealed class LastArmageddonCodec : IIndexedCodecPlugin
{
    public CodecInfo Info { get; } = new()
    {
        Name = "Last Armageddon Font",
        Layout = CodecLayout.Single,
        ColorDepth = 1,
        DefaultWidth = 8 * 32,
        DefaultHeight = 8
    };

    public int GetStorageBits(int width, int height) => 0x3000;

    public void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height)
    {
        var bitCount = encoded.Length * 8;
        var bit = 0;

        for (int i = 0; i < width / 8; i++)
        {
            // Each character starts with a row mask and an unused byte
            if (bit + 16 > bitCount)
                return;

            int rowMask = SampleBits.ReadByte(encoded, bit);
            bit += 16;

            for (int yPos = height - 1; rowMask > 0 && yPos >= 0; yPos--, rowMask >>= 1)
            {
                if ((rowMask & 0x1) != 0)
                    continue;

                for (int x = 7; x >= 0; x--)
                {
                    if (bit >= bitCount)
                        return;

                    pixels[yPos * width + i * 8 + x] = (byte)SampleBits.ReadBit(encoded, bit++);
                }
            }
        }
    }

    public void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height) =>
        throw new NotSupportedException($"'{Info.Name}' is a read-only codec");
}
