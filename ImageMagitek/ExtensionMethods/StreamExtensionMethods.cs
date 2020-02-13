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

        /*public static byte[] ReadShifted(this FileStream file, FileBitAddress address, int numBits, bool Seek)
        {
            if(Seek)
                file.Seek(address.FileOffset, SeekOrigin.Begin);

            using (BinaryReader br = new BinaryReader(file, new UTF8Encoding(), true))
            {
                if (address.BitOffset == 0) // Byte-aligned, whole-byte read
                    return br.ReadBytes(numBits / 8);
            }
        } */
    }
}
