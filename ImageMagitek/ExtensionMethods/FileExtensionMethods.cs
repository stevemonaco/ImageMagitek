using System;
using System.IO;
using System.Text;

namespace ImageMagitek.ExtensionMethods
{
    /// <summary>
    /// Adds additional methods to FileStream related to bitwise file reading
    /// </summary>
    public static class FileExtensionMethods
    {
        public static byte[] ReadUnshifted(this FileStream file, FileBitAddress address, int numBits, bool Seek)
        {
            if (Seek)
                file.Seek(address.FileOffset, SeekOrigin.Begin);

            using var br = new BinaryReader(file, new UTF8Encoding(), true);

            if (address.BitOffset == 0 && (numBits % 8 == 0)) // Byte-aligned, whole-byte read
                return br.ReadBytes(numBits / 8);
            else if (address.BitOffset != 0) // Byte-unaligned read
            {
                int numBytes = (numBits + address.BitOffset + 7) / 8; // Add 7 to take advantage of integer truncation instead of using doubles and Math.Ceiling

                byte[] retArray = br.ReadBytes(numBytes);
                byte premaskbits = (byte)((1 << (8 - address.BitOffset)) - 1);
                retArray[0] &= premaskbits;
                byte postmaskbits = (byte)(premaskbits ^ 0xFF);
                retArray[numBytes - 1] &= postmaskbits;

                return br.ReadBytes(numBytes);
            }
            else
                throw new Exception();
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
