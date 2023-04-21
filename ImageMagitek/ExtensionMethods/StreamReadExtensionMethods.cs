using System;
using System.IO;
using System.Runtime.InteropServices;
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
        await stream.ReadUnshiftedAsync(address, readBits, buffer);
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
        await stream.ReadAsync(readBuffer);

        MaskUnshiftedEndBytes(buffer.Span, skipBits, readBits, readBytes);
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
        stream.Read(readBuffer);

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
        await stream.ReadShiftedAsync(address.BitOffset, readBits, buffer);
    }

    private static async ValueTask ReadShiftedAsync(this Stream stream, int skipBits, int readBits, Memory<byte> buffer)
    {
        if (readBits < 0)
            throw new ArgumentOutOfRangeException($"{nameof(ReadUnshifted)} parameter '{nameof(readBits)}' ({readBits}) must be positive");
        if (skipBits is > 7 or < 0)
            throw new ArgumentOutOfRangeException($"{nameof(ReadUnshifted)} parameter '{nameof(skipBits)}' ({skipBits}) is not within the valid range [0-7]");

        if (skipBits == 0)
        {
            await stream.ReadUnshiftedAsync(skipBits, readBits, buffer);
            return;
        }

        int totalReadBytes = (skipBits + readBits + 7) / 8;
        int firstReadBytes = (readBits + 7) / 8;

        if (buffer.Length < firstReadBytes)
            throw new ArgumentException($"{nameof(ReadUnshifted)} parameter '{nameof(buffer)}' has insufficient length ({buffer.Length}) than required ({firstReadBytes})");

        if (!MemoryMarshal.TryGetArray<byte>(buffer, out var array))
            throw new InvalidOperationException($"{nameof(ReadShiftedAsync)} could not obtain an array from {nameof(MemoryMarshal.TryGetArray)}");

        if (totalReadBytes == 1)
        {
            var readBuffer = buffer[..totalReadBytes];
            var lastByte = stream.ReadByte();
            lastByte = (lastByte >> (8 - (skipBits + readBits)));
            lastByte = (lastByte << (8 - readBits));
            array[0] = (byte)lastByte;
        }
        else if (totalReadBytes == firstReadBytes)
        {
            var readBuffer = buffer[..totalReadBytes];
            await stream.ReadAsync(readBuffer);
            buffer.Span.ShiftLeft(skipBits);

            var lastBits = (skipBits + readBits) - ((totalReadBytes - 1) * 8);
            var mask = ((1 << lastBits) - 1) << (8 - lastBits);
            buffer.Span[totalReadBytes - 1] &= (byte) mask;
        }
        else
        {
            var readBuffer = buffer[..firstReadBytes];
            stream.Read(array);
            buffer.Span.ShiftLeft(skipBits);

            var lastByte = stream.ReadByte();
            var lastBits = (skipBits + readBits) - (firstReadBytes * 8);
            lastByte = lastByte >> (8 - lastBits);
            lastByte = lastByte << (skipBits - lastBits);

            buffer.Span[firstReadBytes - 1] |= (byte)lastByte;
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
        stream.ReadShifted(address.BitOffset, readBits, buffer);
    }

    private static void ReadShifted(this Stream stream, int skipBits, int readBits, Span<byte> buffer)
    {
        if (readBits < 0)
            throw new ArgumentOutOfRangeException($"{nameof(ReadUnshifted)} parameter '{nameof(readBits)}' ({readBits}) must be positive");
        if (skipBits is > 7 or < 0)
            throw new ArgumentOutOfRangeException($"{nameof(ReadUnshifted)} parameter '{nameof(skipBits)}' ({skipBits}) is not within the valid range [0-7]");

        if (skipBits == 0)
        {
            stream.ReadUnshifted(skipBits, readBits, buffer);
            return;
        }

        int totalReadBytes = (skipBits + readBits + 7) / 8;
        int firstReadBytes = (readBits + 7) / 8;

        if (buffer.Length < firstReadBytes)
            throw new ArgumentException($"{nameof(ReadUnshifted)} parameter '{nameof(buffer)}' has insufficient length ({buffer.Length}) than required ({firstReadBytes})");

        if (totalReadBytes == 1)
        {
            var readBuffer = buffer[..totalReadBytes];
            var lastByte = stream.ReadByte();
            lastByte = (lastByte >> (8 - (skipBits + readBits)));
            lastByte = (lastByte << (8 - readBits));
            buffer[0] = (byte)lastByte;
        }
        else if (totalReadBytes == firstReadBytes)
        {
            var readBuffer = buffer[..totalReadBytes];
            stream.Read(readBuffer);
            buffer.ShiftLeft(skipBits);

            var lastBits = (skipBits + readBits) - ((totalReadBytes - 1) * 8);
            var mask = ((1 << lastBits) - 1) << (8 - lastBits);
            buffer[totalReadBytes - 1] = (byte)(buffer[totalReadBytes - 1] & mask);
        }
        else
        {
            var readBuffer = buffer[..firstReadBytes];
            stream.Read(readBuffer);
            buffer.ShiftLeft(skipBits);

            var lastByte = stream.ReadByte();
            var lastBits = (skipBits + readBits) - (firstReadBytes * 8);
            lastByte = lastByte >> (8 - lastBits);
            lastByte = lastByte << (skipBits - lastBits);

            buffer[firstReadBytes - 1] |= (byte)lastByte;
        }
    }
}
