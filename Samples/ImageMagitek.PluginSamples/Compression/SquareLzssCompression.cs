using System;
using System.IO;
using ImageMagitek.Plugins;

namespace ImageMagitek.PluginSamples;

/// <summary>
/// Square's SNES LZSS, used by Final Fantasy V and VI: a 2-byte block length, then flag bytes read LSB-first where 1 is a
/// literal and 0 is a 2-byte reference into a 2 KiB ring buffer that starts zeroed with its write position at 0x7DE
/// </summary>
public sealed class SquareLzssCompression : ICompressionPlugin
{
    private const int BufferSize = 0x800;
    private const int BufferStart = 0x7DE;

    public CompressionInfo Info { get; } = new()
    {
        Name = "Square LZSS",
        Description = "Final Fantasy V/VI (SNES) LZSS with a 2-byte length header"
    };

    public DecompressionResult Decompress(ReadOnlySpan<byte> input)
    {
        if (input.Length < 2)
            throw new InvalidDataException("A Square LZSS block needs a 2-byte length header");

        var blockLength = input[0] | input[1] << 8;
        if (blockLength < 2 || blockLength > input.Length)
            throw new InvalidDataException($"The Square LZSS block length 0x{blockLength:X} does not fit in the 0x{input.Length:X} bytes available");

        var buffer = new byte[BufferSize];
        var bufferPos = BufferStart;
        using var output = new MemoryStream();
        var src = 2;

        while (src < blockLength)
        {
            var flags = input[src++];

            for (int bit = 0; bit < 8 && src < blockLength; bit++)
            {
                if ((flags & (1 << bit)) != 0)
                {
                    Emit(input[src++]);
                    continue;
                }

                if (src + 1 >= blockLength)
                    throw new InvalidDataException("A Square LZSS reference is cut off by the end of the block");

                var b1 = input[src++];
                var b2 = input[src++];
                var position = b1 | (b2 & 0xE0) << 3;
                var length = (b2 & 0x1F) + 3;

                for (int i = 0; i < length; i++)
                    Emit(buffer[(position + i) % BufferSize]);
            }
        }

        return new DecompressionResult(output.ToArray(), blockLength);

        void Emit(byte value)
        {
            output.WriteByte(value);
            buffer[bufferPos] = value;
            bufferPos = (bufferPos + 1) % BufferSize;
        }
    }
}
