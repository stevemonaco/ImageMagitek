using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ImageMagitek
{
    /// <summary>
    /// Stream class with specific features for bit reading and writing
    /// </summary>
    public class BitStream
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
        public static BitStream OpenRead(BinaryReader br, int DataBits, int FirstByteBits) =>
            BitStream.OpenRead(br.BaseStream, DataBits, FirstByteBits);

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
                throw new InvalidOperationException($"{nameof(ReadBit)} does not have read access");
            if (bitsremaining == 0)
                throw new EndOfStreamException($"{nameof(ReadBit)} read past end of stream");

            if (bitindex == 0)
            {
                index++;
                if (index == Data.Length)
                    throw new EndOfStreamException($"{nameof(ReadBit)} read past end of stream");

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
                throw new EndOfStreamException($"{nameof(ReadByte)} read past end of stream");

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
                throw new ArgumentOutOfRangeException($"{nameof(ReadBits)} parameter {nameof(numBits)} ({numBits}) is out of range");

            if (numBits > bitsremaining)
                throw new EndOfStreamException($"{nameof(ReadBits)} read past end of stream");

            int numCycles;

            if (bitindex == 0)
            {
                index++;
                bitindex = 8;
            }

            if (bitindex >= numBits)
                numCycles = 1;
            else
                numCycles = 1 + (numBits - bitindex + 7) / 8;

            int bitsRead = 0; // Number of bits read so far
            var result = 0;

            for(int i = 0; i < numCycles; i++)
            {
                if (bitsRead + bitindex > numBits) // Do a partial read
                {
                    int bitsToRead = numBits - bitsRead;

                    int mask = ((1 << bitsToRead) - 1); // Make mask for the bits to be read
                    mask <<= (bitindex - bitsToRead); // Shift mask to the bit index

                    result <<= bitsToRead;
                    var value = (Data[index] & mask) >> (8 - bitsToRead);
                    result |= value;

                    index++;
                    bitsremaining -= bitsToRead;
                    bitindex -= bitsToRead;
                }
                else // Read entirety of remaining byte
                {
                    int mask = (1 << bitindex) - 1;
                    result <<= bitindex;
                    result |= (Data[index] & mask);

                    index++;
                    bitsremaining -= bitindex;
                    numBits -= bitindex;
                    bitindex = 8;
                }
            }

            return result;
        }

        public void WriteBit(byte bit)
        {
            if ((bit & 0xFE) > 1)
                throw new ArgumentOutOfRangeException();
            if (Access != BitStreamAccess.Write && Access != BitStreamAccess.ReadWrite)
                throw new InvalidOperationException($"{nameof(WriteBit)} does not have write access");
            if (bitsremaining == 0)
                throw new EndOfStreamException($"{nameof(WriteBit)} wrote past end of stream");

            if(bitindex == 0)
            {
                if (index == Data.Length)
                    throw new EndOfStreamException($"{nameof(WriteBit)} wrote past end of stream");

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
            throw new NotImplementedException();
            //if(bitindex != 8) // Some work has been done
            //{
            //}
        }
    }
}
