using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ImageMagitek.Colors;

namespace ImageMagitek;

/// <summary>
/// Reads and writes paletted (color type 3) PNGs with their exact palette indices.
/// ImageSharp cannot preserve indices when the palette contains duplicate colors, nor expose them on load.
/// </summary>
public static class IndexedPngFile
{
    private static readonly byte[] _signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] _crcTable = CreateCrcTable();

    /// <summary>
    /// Writes an 8-bit paletted PNG. The PLTE chunk covers <paramref name="palette"/> and any higher index used by the image.
    /// </summary>
    public static void Write(string path, byte[] indices, int width, int height, IReadOnlyList<ColorRgba32> palette)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(indices.Length, width * height);

        var entries = Math.Min(Math.Max(palette.Count, indices.Take(width * height).Max() + 1), 256);
        var colors = Enumerable.Range(0, entries)
            .Select(i => i < palette.Count ? palette[i] : new ColorRgba32(0, 0, 0, 255))
            .ToArray();

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        stream.Write(_signature);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 3;
        WriteChunk(stream, "IHDR", header);

        WriteChunk(stream, "PLTE", colors.SelectMany(c => new[] { c.R, c.G, c.B }).ToArray());

        var lastTranslucent = Array.FindLastIndex(colors, c => c.A < 255);
        if (lastTranslucent >= 0)
            WriteChunk(stream, "tRNS", colors.Take(lastTranslucent + 1).Select(c => c.A).ToArray());

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (int y = 0; y < height; y++)
            {
                zlib.WriteByte(0);
                zlib.Write(indices, y * width, width);
            }
        }
        WriteChunk(stream, "IDAT", compressed.ToArray());
        WriteChunk(stream, "IEND", []);
    }

    /// <summary>
    /// Reads a non-interlaced paletted PNG at bit depth 1, 2, 4 or 8. Returns false for any other PNG.
    /// </summary>
    public static bool TryRead(string path, out byte[] indices, out ColorRgba32[] palette)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return TryReadCore(stream, out indices, out palette);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or OverflowException
            or ArgumentException or IndexOutOfRangeException)
        {
            indices = [];
            palette = [];
            return false;
        }
    }

    private static bool TryReadCore(Stream stream, out byte[] indices, out ColorRgba32[] palette)
    {
        indices = [];
        palette = [];

        Span<byte> signature = stackalloc byte[8];
        stream.ReadExactly(signature);
        if (!signature.SequenceEqual(_signature))
            return false;

        int width = 0, height = 0, bitDepth = 0;
        byte[]? plte = null;
        byte[]? trns = null;
        using var idat = new MemoryStream();
        Span<byte> chunkHeader = stackalloc byte[8];

        while (true)
        {
            stream.ReadExactly(chunkHeader);
            var length = BinaryPrimitives.ReadInt32BigEndian(chunkHeader);
            var type = Encoding.ASCII.GetString(chunkHeader[4..]);
            if (length < 0)
                return false;

            var data = new byte[length];
            stream.ReadExactly(data);
            stream.ReadExactly(chunkHeader[..4]);

            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data);
                height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4));
                bitDepth = data[8];
                var colorType = data[9];
                var interlace = data[12];

                if (colorType != 3 || interlace != 0 || bitDepth is not (1 or 2 or 4 or 8) || width <= 0 || height <= 0)
                    return false;
            }
            else if (type == "PLTE")
                plte = data;
            else if (type == "tRNS")
                trns = data;
            else if (type == "IDAT")
                idat.Write(data);
            else if (type == "IEND")
                break;
        }

        if (width == 0 || plte is null || plte.Length % 3 != 0)
            return false;

        var stride = checked(width * bitDepth + 7) / 8;
        var filtered = new byte[checked((stride + 1) * height)];
        idat.Position = 0;
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress))
            zlib.ReadExactly(filtered);

        var colors = new ColorRgba32[plte.Length / 3];
        for (int i = 0; i < colors.Length; i++)
        {
            var alpha = trns is not null && i < trns.Length ? trns[i] : (byte)255;
            colors[i] = new ColorRgba32(plte[i * 3], plte[i * 3 + 1], plte[i * 3 + 2], alpha);
        }

        var result = new byte[width * height];
        var previous = new byte[stride];
        var current = new byte[stride];
        var pixelsPerByte = 8 / bitDepth;
        var mask = (1 << bitDepth) - 1;

        for (int y = 0; y < height; y++)
        {
            var rowStart = y * (stride + 1);
            var filter = filtered[rowStart];
            filtered.AsSpan(rowStart + 1, stride).CopyTo(current);

            if (!Unfilter(filter, current, previous))
                return false;

            for (int x = 0; x < width; x++)
            {
                var packed = current[x / pixelsPerByte];
                var shift = 8 - bitDepth * (x % pixelsPerByte + 1);
                var index = (packed >> shift) & mask;

                if (index >= colors.Length)
                    return false;

                result[y * width + x] = (byte)index;
            }

            (previous, current) = (current, previous);
        }

        indices = result;
        palette = colors;
        return true;
    }

    private static bool Unfilter(byte filter, Span<byte> row, ReadOnlySpan<byte> previous)
    {
        for (int i = 0; i < row.Length; i++)
        {
            int left = i > 0 ? row[i - 1] : 0;
            int up = previous[i];
            int upLeft = i > 0 ? previous[i - 1] : 0;

            row[i] += filter switch
            {
                1 => (byte)left,
                2 => (byte)up,
                3 => (byte)((left + up) / 2),
                4 => Paeth(left, up, upLeft),
                _ => (byte)0
            };
        }

        return filter <= 4;
    }

    private static byte Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc)
            return (byte)a;
        return pb <= pc ? (byte)b : (byte)c;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        stream.Write(buffer);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        var crc = UpdateCrc(UpdateCrc(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc);
        stream.Write(buffer);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
