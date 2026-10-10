using System;
using ImageMagitek.Plugins;

namespace ImageMagitek.PluginSamples;

/// <summary>
/// In-game, this font is a 16px tall VWF
/// In-ROM, this 1bpp font is stored rotated right at 90 degrees. Each character has one byte of metadata before it,
/// corresponding to the character's width. Each line of the character is stored in little endian and needs swapped.
/// The font location is 0x2AE31
/// This codec decodes the entirety of the font into one element so an exported image can be rotated for proper viewing.
/// This codec is view-only, adds two pixels of spacing, and does not support editing.
/// </summary>
public sealed class MarmaladeBoyCodec : IIndexedCodecPlugin
{
    public CodecInfo Info { get; } = new()
    {
        Name = "Marmalade Boy Font",
        Layout = CodecLayout.Single,
        ColorDepth = 1,
        DefaultWidth = 16,
        DefaultHeight = 10000
    };

    public int GetStorageBits(int width, int height) => 126820;

    public void Decode(ReadOnlySpan<byte> encoded, Span<byte> pixels, int width, int height)
    {
        var bitCount = encoded.Length * 8;
        var bit = 0;
        var yPos = 0;

        while (bit + 8 <= bitCount)
        {
            int characterWidth = SampleBits.ReadByte(encoded, bit);
            bit += 8;

            if (characterWidth == 0)
                return;

            for (int y = 0; y < characterWidth; y++, yPos++)
            {
                if (yPos >= height)
                    return;

                // Each line is two bytes stored little endian, so the second byte holds the left half
                for (int i = 0; i < 16; i++)
                {
                    if (bit >= bitCount)
                        return;

                    pixels[yPos * width + (i + 8) % 16] = (byte)SampleBits.ReadBit(encoded, bit++);
                }
            }

            yPos += 2;
        }
    }

    public void Encode(ReadOnlySpan<byte> pixels, Span<byte> encoded, int width, int height) =>
        throw new NotSupportedException($"'{Info.Name}' is a read-only codec");
}
