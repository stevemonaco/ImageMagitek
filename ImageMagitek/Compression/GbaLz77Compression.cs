using System;
using System.IO;
using ImageMagitek.Plugins;

namespace ImageMagitek.Compression;

/// <summary>
/// The GBA and NDS BIOS LZ77 format (type 0x10), as used by <c>LZ77UnCompWram</c>
/// </summary>
public sealed class GbaLz77Compression : ICompressionPlugin
{
    public CompressionInfo Info { get; } = new()
    {
        Name = "GBA LZ77",
        Description = "GBA/NDS BIOS LZ77 (header 0x10)"
    };

    public DecompressionResult Decompress(ReadOnlySpan<byte> input)
    {
        if (input.Length < 4 || input[0] != 0x10)
            throw new InvalidDataException($"A GBA LZ77 block starts with 0x10, not 0x{(input.Length > 0 ? input[0] : 0):X2}");

        var size = input[1] | input[2] << 8 | input[3] << 16;
        var output = new byte[size];
        var outPos = 0;
        var src = 4;

        while (outPos < size)
        {
            var flags = ReadByte(input, ref src);

            for (int bit = 7; bit >= 0 && outPos < size; bit--)
            {
                if ((flags & (1 << bit)) == 0)
                {
                    output[outPos++] = ReadByte(input, ref src);
                    continue;
                }

                var b1 = ReadByte(input, ref src);
                var b2 = ReadByte(input, ref src);
                var length = (b1 >> 4) + 3;
                var distance = ((b1 & 0xF) << 8 | b2) + 1;

                if (distance > outPos)
                    throw new InvalidDataException($"A GBA LZ77 back-reference at output 0x{outPos:X} reaches {distance} bytes back, before the start of the data");

                for (int i = 0; i < length && outPos < size; i++, outPos++)
                    output[outPos] = output[outPos - distance];
            }
        }

        return new DecompressionResult(output, src);
    }

    private static byte ReadByte(ReadOnlySpan<byte> input, ref int position)
    {
        if (position >= input.Length)
            throw new InvalidDataException("The GBA LZ77 block ends before its declared size is reached");

        return input[position++];
    }
}
