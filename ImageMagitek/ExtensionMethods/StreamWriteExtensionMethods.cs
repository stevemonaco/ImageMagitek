using System;
using System.IO;
using System.Threading.Tasks;

namespace ImageMagitek.ExtensionMethods;

public static class StreamWriteExtensionMethods
{
    public static async ValueTask WriteUnshiftedAsync(this Stream stream, BitAddress address, int writeBits, ReadOnlyMemory<byte> writeBuffer)
    {
        stream.Seek(address.ByteOffset, SeekOrigin.Begin);
        await stream.WriteUnshiftedAsync(address.BitOffset, writeBits, writeBuffer);
    }

    private static async ValueTask WriteUnshiftedAsync(this Stream stream, int skipBits, int writeBits, ReadOnlyMemory<byte> writeBuffer)
    {
        int totalBytes = (skipBits + writeBits + 7) / 8;

        if (totalBytes == 1)
        {
            var firstByte = (byte)stream.ReadByte();
            stream.Seek(-1, SeekOrigin.Current);

            var merged = MergeByte(firstByte, writeBuffer.Span[0], skipBits, writeBits);
            stream.WriteByte((byte)merged);
            return;
        }

        int writtenBytes = 0;

        if (skipBits != 0)
        {
            var firstByte = (byte)stream.ReadByte();
            stream.Seek(-1, SeekOrigin.Current);

            var merged = MergeByte(firstByte, writeBuffer.Span[0], skipBits, 8 - skipBits);
            stream.WriteByte(merged);
            writtenBytes++;
        }

        int lastBits = (skipBits + writeBits) % 8;

        if (lastBits != 0)
        {
            var writeSlice = writeBuffer.Slice(writtenBytes, totalBytes - writtenBytes - 1);
            await stream.WriteAsync(writeSlice);

            var lastByte = (byte)stream.ReadByte();
            stream.Seek(-1, SeekOrigin.Current);

            var merged = MergeByte(lastByte, writeBuffer.Span[totalBytes - 1], 0, lastBits);
            stream.WriteByte((byte)merged);
        }
        else
        {
            var writeSlice = writeBuffer[writtenBytes..totalBytes];
            await stream.WriteAsync(writeSlice);
        }
    }


    public static void WriteUnshifted(this Stream stream, BitAddress address, int writeBits, ReadOnlySpan<byte> writeBuffer)
    {
        stream.Seek(address.ByteOffset, SeekOrigin.Begin);
        stream.WriteUnshifted(address.BitOffset, writeBits, writeBuffer);
    }

    private static void WriteUnshifted(this Stream stream, int skipBits, int writeBits, ReadOnlySpan<byte> writeBuffer)
    {
        int totalBytes = (skipBits + writeBits + 7) / 8;

        if (totalBytes == 1)
        {
            var firstByte = (byte)stream.ReadByte();
            stream.Seek(-1, SeekOrigin.Current);

            var merged = MergeByte(firstByte, writeBuffer[0], skipBits, writeBits);
            stream.WriteByte((byte)merged);
            return;
        }

        int writtenBytes = 0;

        if (skipBits != 0)
        {
            var firstByte = (byte)stream.ReadByte();
            stream.Seek(-1, SeekOrigin.Current);

            var merged = MergeByte(firstByte, writeBuffer[0], skipBits, 8 - skipBits);
            stream.WriteByte((byte)merged);
            writtenBytes++;
        }

        int lastBits = (skipBits + writeBits) % 8;

        if (lastBits != 0)
        {
            var span = writeBuffer.Slice(writtenBytes, totalBytes - writtenBytes - 1);
            stream.Write(span);

            var lastByte = (byte)stream.ReadByte();
            stream.Seek(-1, SeekOrigin.Current);

            var merged = MergeByte(lastByte, writeBuffer[totalBytes - 1], 0, lastBits);
            stream.WriteByte((byte)merged);
        }
        else
        {
            var span = writeBuffer.Slice(writtenBytes, totalBytes - writtenBytes);
            stream.Write(span);
        }
    }

    public static void WriteShifted(this Stream stream, BitAddress address, int writeBits, ReadOnlySpan<byte> writeBuffer)
    {
        stream.Seek(address.ByteOffset, SeekOrigin.Begin);
        stream.WriteShifted(address.BitOffset, writeBits, writeBuffer);
    }

    private static void WriteShifted(this Stream stream, int skipBits, int writeBits, ReadOnlySpan<byte> writeBuffer)
    {
        if (skipBits == 0)
        {
            stream.WriteUnshifted(skipBits, writeBits, writeBuffer);
            return;
        }

        var shifted = ShiftForWrite(skipBits, writeBits, writeBuffer);
        stream.WriteUnshifted(skipBits, writeBits, shifted);
    }

    public static async ValueTask WriteShiftedAsync(this Stream stream, BitAddress address, int writeBits, ReadOnlyMemory<byte> writeBuffer)
    {
        stream.Seek(address.ByteOffset, SeekOrigin.Begin);

        if (address.BitOffset == 0)
        {
            await stream.WriteUnshiftedAsync(0, writeBits, writeBuffer);
            return;
        }

        var shifted = ShiftForWrite(address.BitOffset, writeBits, writeBuffer.Span);
        await stream.WriteUnshiftedAsync(address.BitOffset, writeBits, shifted);
    }

    private static byte[] ShiftForWrite(int skipBits, int writeBits, ReadOnlySpan<byte> writeBuffer)
    {
        var buffer = new byte[(skipBits + writeBits + 7) / 8];
        writeBuffer[..((writeBits + 7) / 8)].CopyTo(buffer);
        buffer.AsSpan().ShiftRight(skipBits);
        return buffer;
    }

    private static byte MergeByte(byte original, byte write, int skipBits, int writeBits)
    {
        var mask = ((1 << writeBits) - 1) << (8 - skipBits - writeBits);
        write = (byte)(write & mask);
        mask = ~mask;
        var merged = original & mask;
        merged |= write;
        return (byte)merged;
    }
}
