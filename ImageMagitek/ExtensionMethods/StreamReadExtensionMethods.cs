using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace ImageMagitek.ExtensionMethods;

/// <summary>
/// Adds additional methods to Stream related to bitwise reading
/// </summary>
public static class StreamReadExtensionMethods
{
    public static async ValueTask<byte[]> ReadUnshiftedAsync(this Stream stream, BitAddress address, int readBits)
    {
        var readBuffer = new byte[(readBits + address.BitOffset + 7) / 8];
        await stream.ReadUnshiftedAsync(address, readBits, readBuffer);
        return readBuffer;
    }

    public static async ValueTask ReadUnshiftedAsync(this Stream stream, BitAddress address, int readBits, Memory<byte> buffer)
    {
        stream.Seek(address.ByteOffset, SeekOrigin.Begin);
        await stream.ReadUnshiftedAsync(address.BitOffset, readBits, buffer);
    }

    private static async ValueTask ReadUnshiftedAsync(this Stream stream, int skipBits, int readBits, Memory<byte> buffer)
    {
        if (readBits < 0)
            throw new ArgumentOutOfRangeException($"{nameof(ReadUnshiftedAsync)} parameter '{nameof(readBits)}' ({readBits}) must be positive");
        if (skipBits is > 7 or < 0)
            throw new ArgumentOutOfRangeException($"{nameof(ReadUnshiftedAsync)} parameter '{nameof(skipBits)}' ({skipBits}) is not within the valid range [0-7]");

        int readBytes = (skipBits + readBits + 7) / 8;

        if (buffer.Length < readBytes)
            throw new ArgumentException($"{nameof(ReadUnshiftedAsync)} parameter '{nameof(buffer)}' has insufficient length ({buffer.Length}) than required ({readBytes})");

        var readBuffer = buffer[..readBytes];
        await stream.ReadExactlyAsync(readBuffer);

        MaskUnshiftedEndBytes(readBuffer.Span, skipBits, readBits, readBytes);
    }

    public static byte[] ReadUnshifted(this Stream stream, BitAddress address, int readBits)
    {
        var readBuffer = new byte[(readBits + address.BitOffset + 7) / 8];
        stream.ReadUnshifted(address, readBits, readBuffer);
        return readBuffer;
    }

    public static void ReadUnshifted(this Stream stream, BitAddress address, int readBits, Span<byte> buffer)
    {
        stream.Seek(address.ByteOffset, SeekOrigin.Begin);
        stream.ReadUnshifted(address.BitOffset, readBits, buffer);
    }

    private static void ReadUnshifted(this Stream stream, int skipBits, int readBits, Span<byte> buffer)
    {
        if (readBits < 0)
            throw new ArgumentOutOfRangeException($"{nameof(ReadUnshifted)} parameter '{nameof(readBits)}' ({readBits}) must be positive");
        if (skipBits is > 7 or < 0)
            throw new ArgumentOutOfRangeException($"{nameof(ReadUnshifted)} parameter '{nameof(skipBits)}' ({skipBits}) is not within the valid range [0-7]");

        int readBytes = (skipBits + readBits + 7) / 8;

        if (buffer.Length < readBytes)
            throw new ArgumentException($"{nameof(ReadUnshifted)} parameter '{nameof(buffer)}' has insufficient length ({buffer.Length}) than required ({readBytes})");

        var readBuffer = buffer.Slice(0, readBytes);
        stream.ReadExactly(readBuffer);

        MaskUnshiftedEndBytes(readBuffer, skipBits, readBits, readBytes);
    }

    private static void MaskUnshiftedEndBytes(Span<byte> buffer, int skipBits, int readBits, int readBytes)
    {
        // Mask bits skipped on the first byte
        int mask = (1 << (8 - skipBits)) - 1;
        buffer[0] = (byte)(buffer[0] & mask);

        // Mask bits skipped on the last byte
        int lastBits = (readBytes * 8) - readBits - skipBits;
        mask = 0xFF ^ ((1 << lastBits) - 1);
        buffer[^1] = (byte)(buffer[^1] & mask);
    }

    public static async ValueTask<byte[]> ReadShiftedAsync(this Stream stream, BitAddress address, int readBits)
    {
        var readBuffer = new byte[(readBits + 7) / 8];
        await stream.ReadShiftedAsync(address, readBits, readBuffer);
        return readBuffer;
    }

    public static async ValueTask ReadShiftedAsync(this Stream stream, BitAddress address, int readBits, Memory<byte> buffer)
    {
        stream.Seek(address.ByteOffset, SeekOrigin.Begin);
        int skipBits = address.BitOffset;
        ValidateShiftedRead(skipBits, readBits, buffer.Length);

        if (skipBits == 0)
        {
            await stream.ReadUnshiftedAsync(0, readBits, buffer);
            return;
        }

        int totalReadBytes = (skipBits + readBits + 7) / 8;
        var raw = ArrayPool<byte>.Shared.Rent(totalReadBytes);
        try
        {
            await stream.ReadExactlyAsync(raw.AsMemory(0, totalReadBytes));
            ShiftIntoBuffer(raw.AsSpan(0, totalReadBytes), skipBits, readBits, buffer.Span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(raw);
        }
    }

    public static byte[] ReadShifted(this Stream stream, BitAddress address, int readBits)
    {
        var readBuffer = new byte[(readBits + 7) / 8];
        stream.ReadShifted(address, readBits, readBuffer);
        return readBuffer;
    }

    public static void ReadShifted(this Stream stream, BitAddress address, int readBits, Span<byte> buffer)
    {
        stream.Seek(address.ByteOffset, SeekOrigin.Begin);
        int skipBits = address.BitOffset;
        ValidateShiftedRead(skipBits, readBits, buffer.Length);

        if (skipBits == 0)
        {
            stream.ReadUnshifted(0, readBits, buffer);
            return;
        }

        int totalReadBytes = (skipBits + readBits + 7) / 8;
        var raw = ArrayPool<byte>.Shared.Rent(totalReadBytes);
        try
        {
            var rawSpan = raw.AsSpan(0, totalReadBytes);
            stream.ReadExactly(rawSpan);
            ShiftIntoBuffer(rawSpan, skipBits, readBits, buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(raw);
        }
    }

    private static void ValidateShiftedRead(int skipBits, int readBits, int bufferLength)
    {
        if (readBits < 0)
            throw new ArgumentOutOfRangeException(nameof(readBits), $"{nameof(ReadShifted)} parameter '{nameof(readBits)}' ({readBits}) must be positive");
        if (skipBits is > 7 or < 0)
            throw new ArgumentOutOfRangeException(nameof(skipBits), $"{nameof(ReadShifted)} parameter '{nameof(skipBits)}' ({skipBits}) is not within the valid range [0-7]");

        int readBytes = (readBits + 7) / 8;
        if (bufferLength < readBytes)
            throw new ArgumentException($"{nameof(ReadShifted)} parameter 'buffer' has insufficient length ({bufferLength}) than required ({readBytes})");
    }

    /// <summary>
    /// Moves readBits bits that start skipBits into raw so they start at bit 0 of buffer, and clears the trailing bits.
    /// </summary>
    private static void ShiftIntoBuffer(ReadOnlySpan<byte> raw, int skipBits, int readBits, Span<byte> buffer)
    {
        int readBytes = (readBits + 7) / 8;
        if (readBytes == 0)
            return;

        for (int i = 0; i < readBytes; i++)
        {
            int next = i + 1 < raw.Length ? raw[i + 1] : 0;
            buffer[i] = (byte)((raw[i] << skipBits) | (next >> (8 - skipBits)));
        }

        int trailingBits = readBytes * 8 - readBits;
        buffer[readBytes - 1] &= (byte)(0xFF << trailingBits);
    }
}
