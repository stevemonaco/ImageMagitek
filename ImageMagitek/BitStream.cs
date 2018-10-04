using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ImageMagitek
{
    /// <summary>
    /// Struct used to store a file address that does not start on a byte-aligned address
    /// </summary>
    public struct FileBitAddress
    {
        /// <summary>
        /// File offset in bytes
        /// </summary>
        public long FileOffset { get; set; }

        /// <summary>
        /// Number of bits to skip after FileOffset
        /// Valid range is 0-7 inclusive
        /// A zero value would result in a byte-aligned address
        /// </summary>
        public int BitOffset { get; set; }

        public FileBitAddress(long fileOffset, int bitOffset)
        {
            if (bitOffset > 7)
                throw new ArgumentOutOfRangeException();

            FileOffset = fileOffset;
            BitOffset = bitOffset;
        }

        /// <summary>
        /// Construct a new FileBitAddress from the number of bits to the address
        /// </summary>
        /// <param name="bits"></param>
        public FileBitAddress(long bits)
        {
            FileOffset = bits / 8;
            BitOffset = (int)(bits % 8);
        }

        public long Bits()
        {
            return FileOffset * 8 + BitOffset;
        }

        /// <summary>
        /// Casts a long into a FileBitAddress
        /// </summary>
        /// <param name="Address">Number of bits</param>
        public static implicit operator FileBitAddress(long Address)
        {
            return new FileBitAddress(Address / 8, (int)(Address % 8));
        }

        /// <summary>
        /// Adds two FileBitAddress objects and returns the result
        /// </summary>
        /// <param name="Address1"></param>
        /// <param name="Address2"></param>
        /// <returns></returns>
        public static FileBitAddress operator+(FileBitAddress Address1, FileBitAddress Address2)
        {
            long bits = Address1.Bits() + Address2.Bits();
            return new FileBitAddress(bits);
        }

        /// <summary>
        /// Adds a specified number of bits to a FileBitAddress object
        /// </summary>
        /// <param name="Address"></param>
        /// <param name="Offset">Number of bits to advance the address</param>
        /// <returns></returns>
        public static FileBitAddress operator +(FileBitAddress Address, long Offset)
        {
            long bits = Address.Bits() + Offset;
            return new FileBitAddress(bits);
        }

        /// <summary>
        /// Subtracts two FileBitAddress objects and returns the result
        /// </summary>
        /// <param name="Address1"></param>
        /// <param name="Address2"></param>
        /// <returns></returns>
        public static FileBitAddress operator -(FileBitAddress Address1, FileBitAddress Address2)
        {
            long bits = Address1.Bits() - Address2.Bits();

            return new FileBitAddress(bits);
        }

        /// <summary>
        /// Subtracts a number of bits from a FileBitAddress object
        /// </summary>
        /// <param name="Address"></param>
        /// <param name="Offset"></param>
        /// <returns></returns>
        public static FileBitAddress operator -(FileBitAddress Address, long Offset)
        {
            long addressbits = Address.Bits();
            long retbits = addressbits - Offset;

            return new FileBitAddress(retbits);
        }

        public static bool operator <(FileBitAddress Address1, FileBitAddress Address2)
        {
            return Address1.Bits() < Address2.Bits();
        }

        public static bool operator <=(FileBitAddress Address1, FileBitAddress Address2)
        {
            return Address1.Bits() <= Address2.Bits();
        }

        public static bool operator >(FileBitAddress Address1, FileBitAddress Address2)
        {
            return Address1.Bits() > Address2.Bits();
        }
        public static bool operator >=(FileBitAddress Address1, FileBitAddress Address2)
        {
            return Address1.Bits() >= Address2.Bits();
        }
    }

    /// <summary>
    /// Adds additional methods to FileStream related to bitwise file reading
    /// </summary>
    public static class FileExtensionMethods
    {
        public static byte[] ReadUnshifted(this FileStream file, FileBitAddress address, int numBits, bool Seek)
        {
            if(Seek)
                file.Seek(address.FileOffset, SeekOrigin.Begin);

            using (BinaryReader br = new BinaryReader(file, new UTF8Encoding(), true))
            {
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

    /// <summary>
    /// Stream class with specific features for bit reading and writing
    /// </summary>
    internal class BitStream
    {
        private enum BitStreamAccess { Read, Write, ReadWrite };

        /// <summary>
        /// Current working bit index
        /// </summary>
        private int bitindex;

        /// <summary>
        ///  Current working index into data array where bits are read/written to
        /// </summary>
        private int index;

        /// <summary>
        /// Bits remaining in the stream
        /// </summary>
        private int bitsremaining;

        /// <summary>
        /// Type of access to the stream
        /// </summary>
        private BitStreamAccess Access;

        public byte[] Data
        {
            get { return data; }
            private set { data = value; }
        }
        private byte[] data;

        private BitStream() { }

        /// <summary>
        /// Creates a new Bitstream with the specified array for bit reading
        /// </summary>
        /// <param name="ReadData">Data to be read</param>
        /// <param name="DataBits">Number of valid bits to read in the array</param>
        /// <returns></returns>
        public static BitStream OpenRead(byte[] ReadData, int DataBits)
        {
            BitStream bs = new BitStream();

            bs.Data = ReadData;
            bs.bitsremaining = DataBits;
            bs.bitindex = 8;
            bs.index = 0;
            bs.Access = BitStreamAccess.Read;

            return bs;
        }

        /// <summary>
        /// Creates a new BitStream with its own array from a BinaryReader for bit reading
        /// </summary>
        /// <param name="br">Underlying binary reader for the stream</param>
        /// <param name="DataBits"></param>
        /// <param name="FirstByteBits"></param>
        /// <returns>A readable BitStream instance</returns>
        public static BitStream OpenRead(BinaryReader br, int DataBits, int FirstByteBits)
        {
            BitStream bs = new BitStream();

            int ReadLength = (int)Math.Ceiling((DataBits + (8 - FirstByteBits)) / 8.0);
            bs.Data = br.ReadBytes(ReadLength);
            byte mask = (byte)((1 << FirstByteBits) - 1);
            bs.Data[0] = (byte)(bs.Data[0] & mask);

            bs.bitindex = FirstByteBits;
            bs.bitsremaining = DataBits;
            bs.index = 0;
            bs.Access = BitStreamAccess.Read;

            return bs;
        }

        /// <summary>
        /// Creates a new BitStream with its own array from a Stream for bit reading
        /// </summary>
        /// <param name="br">Underlying stream</param>
        /// <param name="DataBits"></param>
        /// <param name="FirstByteBits"></param>
        /// <returns>A readable BitStream instance</returns>
        public static BitStream OpenRead(Stream stream, int DataBits, int FirstByteBits)
        {
            BitStream bs = new BitStream();

            BinaryReader br = new BinaryReader(stream);

            int ReadLength = (int)Math.Ceiling((DataBits + (8 - FirstByteBits)) / 8.0);
            bs.Data = br.ReadBytes(ReadLength);
            byte mask = (byte)((1 << FirstByteBits) - 1);
            bs.Data[0] = (byte)(bs.Data[0] & mask);

            bs.bitindex = FirstByteBits;
            bs.bitsremaining = DataBits;
            bs.index = 0;
            bs.Access = BitStreamAccess.Read;

            return bs;
        }

        /// <summary>
        /// Creates a new BitStream for writing bits to an array
        /// </summary>
        /// <param name="DataBits">Size of writable array in bits</param>
        /// <param name="FirstByteBits">Number of bits available for writing in the first byte</param>
        /// <returns>A writable BitStream instance</returns>
        public static BitStream OpenWrite(int DataBits, int FirstByteBits)
        {
            BitStream bs = new BitStream();

            int BufferLength = (int)Math.Ceiling((DataBits + (8 - FirstByteBits)) / 8.0);
            bs.Data = new byte[BufferLength];

            bs.bitindex = FirstByteBits;
            bs.bitsremaining = DataBits;
            bs.index = 0;
            bs.Access = BitStreamAccess.Write;

            return bs;
        }

        /// <summary>
        /// Reads a single bit from the underlying array
        /// </summary>
        /// <returns></returns>
        public int ReadBit()
        {
            if (Access != BitStreamAccess.Read && Access != BitStreamAccess.ReadWrite)
                throw new InvalidOperationException();
            if (bitsremaining == 0)
                throw new EndOfStreamException();

            if (bitindex == 0)
            {
                index++;
                if (index == Data.Length)
                    throw new EndOfStreamException();

                bitindex = 8;
            }

            int bit = (Data[index] >> (bitindex - 1)) & 1;
            bitsremaining--;
            bitindex--;

            return bit;
        }

        public byte ReadByte()
        {
            if (bitsremaining < 8)
                throw new EndOfStreamException();

            return (byte)ReadBits(8);
        }

        /// <summary>
        /// Reads the specified number of bits from the stream
        /// </summary>
        /// <param name="numBits">Number of bits to read between 1 and 32</param>
        /// <returns></returns>
        public int ReadBits(int numBits)
        {
            if (numBits > 32 || numBits < 1)
                throw new ArgumentOutOfRangeException();

            if (numBits > bitsremaining)
                throw new EndOfStreamException();

            int numCycles;

            if (bitsremaining >= numBits)
                numCycles = 1;
            else
                numCycles = 1 + (numBits - bitsremaining + 7) / 8;

            int bitsRead = 0; // Number of bits read so far
            int ret = 0; // Value to be returned

            for(int i = 0; i < numCycles; i++)
            {
                if (bitsRead + bitindex > numBits) // Do a partial read
                {
                    int bitsToRead = numBits - bitsRead;

                    int mask = ((1 << bitsToRead) - 1); // Make mask for bits to read
                    mask <<= (bitindex - bitsToRead); // Shift mask to the bit index

                    ret <<= bitsToRead;
                    ret |= (Data[index] & mask);

                    index++;
                    bitsremaining -= bitsToRead;
                    bitindex = 8;
                }
                else // Read entirety of remaining byte
                {
                    int mask = (1 << bitindex) - 1;
                    ret <<= bitindex;
                    ret |= (Data[index] & mask);

                    index++;
                    bitsremaining -= bitindex;
                    bitindex = 8;
                }
            }

            return ret;
        }

        public void WriteBit(byte bit)
        {
            if ((bit & 0xFE) > 1)
                throw new ArgumentOutOfRangeException();
            if (Access != BitStreamAccess.Write && Access != BitStreamAccess.ReadWrite)
                throw new InvalidOperationException();
            if (bitsremaining == 0)
                throw new EndOfStreamException();

            if(bitindex == 0)
            {
                if (index == Data.Length)
                    throw new EndOfStreamException();

                index++;
                bitindex = 8;
            }

            Data[index] |= (byte)(bit << (bitindex - 1));
            bitsremaining--;
            bitindex--;
        }

        public void WriteBits(int val, int numBits)
        {
            throw new NotImplementedException();
        }

        public void FlushWrites()
        {
            if(bitindex != 8) // Some work has been done
            {

            }
        }
    }
}
