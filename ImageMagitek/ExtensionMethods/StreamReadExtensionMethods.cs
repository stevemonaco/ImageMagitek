using System;
using System.IO;

namespace ImageMagitek.ExtensionMethods;

/// <summary>
/// Adds additional methods to Stream related to bitwise reading
/// </summary>
public static class StreamReadExtensionMethods
{
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
        if (skipBits > 7 || skipBits < 0)
            throw new ArgumentOutOfRangeException($"{nameof(ReadUnshifted)} parameter '{nameof(skipBits)}' ({skipBits}) is not within the valid range [0-7]");

        int readBytes = (skipBits + readBits + 7) / 8;

        if (buffer.Length < readBytes)
            throw new ArgumentException($"{nameof(ReadUnshifted)} parameter '{nameof(buffer)}' has insufficient length ({buffer.Length}) than required ({readBytes})");

        var readBuffer = buffer.Slice(0, readBytes);
        stream.Read(readBuffer);

        // Mask bits skipped on the first byte
        int mask = (1 << (8 - skipBits)) - 1;
        readBuffer[0] = (byte)(readBuffer[0] & mask);

        // Mask bits skipped on the last byte
        int lastBits = (readBytes * 8) - readBits - skipBits;
        mask = 0xFF ^ ((1 << lastBits) - 1);
        readBuffer[^1] = (byte)(readBuffer[^1] & mask);
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
        if (skipBits > 7 || skipBits < 0)
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
            var readBuffer = buffer.Slice(0, totalReadBytes);
            var lastByte = stream.ReadByte();
            lastByte = (lastByte >> (8 - (skipBits + readBits)));
            lastByte = (lastByte << (8 - readBits));
            buffer[0] = (byte)lastByte;
        }
        else if (totalReadBytes == firstReadBytes)
        {
            var readBuffer = buffer.Slice(0, totalReadBytes);
            stream.Read(readBuffer);
            buffer.ShiftLeft(skipBits);

            var lastBits = (skipBits + readBits) - ((totalReadBytes - 1) * 8);
            var mask = ((1 << lastBits) - 1) << (8 - lastBits);
            buffer[totalReadBytes - 1] = (byte)(buffer[totalReadBytes - 1] & mask);
        }
        else
        {
            var readBuffer = buffer.Slice(0, firstReadBytes);
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
