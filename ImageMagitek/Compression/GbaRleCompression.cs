using System;
using System.IO;
using ImageMagitek.Plugins;

namespace ImageMagitek.Compression;

/// <summary>
/// The GBA and NDS BIOS run-length format (type 0x30), as used by <c>RLUnCompWram</c>
/// </summary>
public sealed class GbaRleCompression : ICompressionPlugin
{
    public CompressionInfo Info { get; } = new()
    {
        Name = "GBA RLE",
        Description = "GBA/NDS BIOS run-length (header 0x30)"
    };

    public DecompressionResult Decompress(ReadOnlySpan<byte> input)
    {
        if (input.Length < 4 || input[0] != 0x30)
            throw new InvalidDataException($"A GBA RLE block starts with 0x30, not 0x{(input.Length > 0 ? input[0] : 0):X2}");

        var size = input[1] | input[2] << 8 | input[3] << 16;
        var output = new byte[size];
        var outPos = 0;
        var src = 4;

        while (outPos < size)
        {
            var flag = ReadByte(input, ref src);

            if ((flag & 0x80) != 0)
            {
                var value = ReadByte(input, ref src);
                for (int i = 0; i < (flag & 0x7F) + 3 && outPos < size; i++)
                    output[outPos++] = value;
            }
            else
            {
                for (int i = 0; i < (flag & 0x7F) + 1 && outPos < size; i++)
                    output[outPos++] = ReadByte(input, ref src);
            }
        }

        return new DecompressionResult(output, src);
    }

    private static byte ReadByte(ReadOnlySpan<byte> input, ref int position)
    {
        if (position >= input.Length)
            throw new InvalidDataException("The GBA RLE block ends before its declared size is reached");

        return input[position++];
    }
}
