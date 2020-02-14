using System;
using System.IO;
using System.Text;

namespace ImageMagitek.ExtensionMethods
{
    /// <summary>
    /// Adds additional methods to Stream related to bitwise reading
    /// </summary>
    public static class StreamExtensionMethods
    {
        public static byte[] ReadUnshifted(this Stream stream, FileBitAddress address, int readBits)
        {
            var readBuffer = new byte[(readBits + 7) / 8];
            stream.ReadUnshifted(address, readBits, readBuffer);
            return readBuffer;
        }

        public static void ReadUnshifted(this Stream stream, FileBitAddress address, int readBits, Span<byte> buffer)
        {
            stream.Seek(address.FileOffset, SeekOrigin.Begin);
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
            readBuffer[0] = (byte) (readBuffer[0] & mask);

            // Mask bits skipped on the last byte
            int lastBits = (readBytes * 8) - readBits - skipBits;
            mask = 0xFF ^ ((1 << lastBits) - 1);
            readBuffer[readBytes - 1] = (byte)(readBuffer[readBytes - 1] & mask);
        }

        public static byte[] ReadShifted(this Stream stream, FileBitAddress address, int readBits)
        {
            var readBuffer = new byte[(readBits + 7) / 8];
            stream.ReadShifted(address, readBits, readBuffer);
            return readBuffer;
        }

        public static void ReadShifted(this Stream stream, FileBitAddress address, int readBits, Span<byte> buffer)
        {
            stream.Seek(address.FileOffset, SeekOrigin.Begin);
            stream.ReadShifted(address.BitOffset, readBits, buffer);
        }

        private static void ReadShifted(this Stream stream, int skipBits, int readBits, Span<byte> buffer)
        {
            if (readBits < 0)
                throw new ArgumentOutOfRangeException($"{nameof(ReadUnshifted)} parameter '{nameof(readBits)}' ({readBits}) must be positive");
            if (skipBits > 7 || skipBits < 0)
                throw new ArgumentOutOfRangeException($"{nameof(ReadUnshifted)} parameter '{nameof(skipBits)}' ({skipBits}) is not within the valid range [0-7]");

            int readBytes = (skipBits + readBits + 7) / 8;

            if (buffer.Length < readBytes)
                throw new ArgumentException($"{nameof(ReadUnshifted)} parameter '{nameof(buffer)}' has insufficient length ({buffer.Length}) than required ({readBytes})");

            if (skipBits == 0)
            {
                stream.ReadUnshifted(skipBits, readBits, buffer);
                return;
            }

            if (readBits + skipBits <= 8)
            {
                var fullByte = stream.ReadByte() << skipBits;
                var mask = ((1 << readBits) - 1) << (8 - readBits);
                buffer[0] = (byte)(fullByte & mask);
                return;
            }

            var readBuffer = buffer.Slice(0, readBytes - 1);
            stream.Read(readBuffer);
            buffer.ShiftLeft(skipBits);

            int lastBits = readBits - Math.Max(readBuffer.Length * 8 - skipBits, 0);
            if (lastBits < 8 && lastBits > 0)
            {
                var lastByte = stream.ReadByte();
                var left = lastByte >> (8 - skipBits);
                int rightBits = lastBits - skipBits;
                int rightMask = ((1 << lastBits) - 1) << (8 - lastBits);
                var right = (lastByte << skipBits) & rightMask;
                var leftIndex = Math.Max(buffer.Length - 2, 0);
                buffer[leftIndex] |= (byte)left;
                buffer[buffer.Length - 1] = (byte)right;
            }
        }
    }
}
