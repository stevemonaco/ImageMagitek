using System;
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
        private int BitIndex;

        /// <summary>
        ///  Current working index into data array where bits are read/written to
        /// </summary>
        private int Index;

        /// <summary>
        /// Bits remaining in the stream
        /// </summary>
        private int BitsRemaining;

        /// <summary>
        /// Type of access to the stream
        /// </summary>
        private BitStreamAccess Access;

        private int StreamStartOffset;

        private int StreamEndOffset;

        private int StreamSize { get => StreamEndOffset - StreamStartOffset; }

        public byte[] Data { get; private set; }

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
            bs.BitsRemaining = DataBits;
            bs.BitIndex = 8;
            bs.Index = 0;
            bs.Access = BitStreamAccess.Read;
            bs.StreamStartOffset = 0;
            bs.StreamEndOffset = DataBits;

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

            bs.BitIndex = FirstByteBits;
            bs.BitsRemaining = DataBits;
            bs.Index = 0;
            bs.Access = BitStreamAccess.Read;
            bs.StreamStartOffset = 8 - FirstByteBits;
            bs.StreamEndOffset = DataBits - bs.StreamStartOffset;

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
            int BufferLength = (int)Math.Ceiling((DataBits + (8 - FirstByteBits)) / 8.0);
            var data = new byte[BufferLength];

            return OpenWrite(data, DataBits, FirstByteBits);
        }

        public static BitStream OpenWrite(byte[] Buffer, int DataBits, int FirstByteBits)
        {
            BitStream bs = new BitStream();

            bs.Data = Buffer;

            bs.BitIndex = FirstByteBits;
            bs.Index = 0;
            bs.Access = BitStreamAccess.Write;
            bs.StreamStartOffset = 8 - FirstByteBits;
            bs.StreamEndOffset = DataBits - bs.StreamStartOffset;

            return bs;
        }

        public void SeekAbsolute(int seekBits)
        {
            if (seekBits < 0 || seekBits >= StreamSize)
                throw new ArgumentOutOfRangeException($"{nameof(SeekAbsolute)} parameter '{nameof(seekBits)} is out of range ({seekBits})'");

            Index = (StreamStartOffset + seekBits) / 8;
            BitIndex = 8 - (StreamStartOffset + seekBits) % 8;
            BitsRemaining = StreamEndOffset - (StreamStartOffset + seekBits);
        }

        public void SeekRelative(int seekBits)
        {
            int seekOffset = BitsRemaining + seekBits;

            if (seekOffset < 0 || seekOffset >= StreamSize)
                throw new ArgumentOutOfRangeException($"{nameof(SeekRelative)} parameter '{nameof(seekOffset)} is out of range ({seekOffset})'");

            Index = (StreamStartOffset + seekOffset) / 8;
            BitIndex = 8 - (StreamStartOffset + seekOffset) % 8;
            BitsRemaining = StreamEndOffset - (StreamStartOffset + seekOffset);
        }

        /// <summary>
        /// Reads a single bit from the underlying array
        /// </summary>
        /// <returns></returns>
        public int ReadBit()
        {
            if (Access != BitStreamAccess.Read && Access != BitStreamAccess.ReadWrite)
                throw new InvalidOperationException($"{nameof(ReadBit)} does not have read access");
            if (BitsRemaining == 0)
                throw new EndOfStreamException($"{nameof(ReadBit)} read past end of stream");

            if (BitIndex == 0)
            {
                Index++;
                if (Index == Data.Length)
                    throw new EndOfStreamException($"{nameof(ReadBit)} read past end of stream");

                BitIndex = 8;
            }

            int bit = (Data[Index] >> (BitIndex - 1)) & 1;
            BitsRemaining--;
            BitIndex--;

            return bit;
        }

        public byte ReadByte()
        {
            if (BitsRemaining < 8)
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

            if (numBits > BitsRemaining)
                throw new EndOfStreamException($"{nameof(ReadBits)} read past end of stream");

            int numCycles;

            if (BitIndex == 0)
            {
                Index++;
                BitIndex = 8;
            }

            if (BitIndex >= numBits)
                numCycles = 1;
            else
                numCycles = 1 + (numBits - BitIndex + 7) / 8;

            int bitsRead = 0; // Number of bits read so far
            var result = 0;

            for(int i = 0; i < numCycles; i++)
            {
                if (bitsRead + BitIndex > numBits) // Do a partial read
                {
                    int bitsToRead = numBits - bitsRead;

                    int mask = ((1 << bitsToRead) - 1); // Make mask for the bits to be read
                    mask <<= (BitIndex - bitsToRead); // Shift mask to the bit index

                    result <<= bitsToRead;
                    var value = (Data[Index] & mask) >> (8 - bitsToRead);
                    result |= value;

                    Index++;
                    BitsRemaining -= bitsToRead;
                    BitIndex -= bitsToRead;
                }
                else // Read entirety of remaining byte
                {
                    int mask = (1 << BitIndex) - 1;
                    result <<= BitIndex;
                    result |= (Data[Index] & mask);

                    Index++;
                    BitsRemaining -= BitIndex;
                    numBits -= BitIndex;
                    BitIndex = 8;
                }
            }

            return result;
        }

        public void WriteBit(int bit)
        {
            if (bit > 1)
                throw new ArgumentOutOfRangeException();
            if (Access != BitStreamAccess.Write && Access != BitStreamAccess.ReadWrite)
                throw new InvalidOperationException($"{nameof(WriteBit)} does not have write access");
            if (BitsRemaining == 0)
                throw new EndOfStreamException($"{nameof(WriteBit)} attempted to write past end of stream");

            if(BitIndex == 0)
            {
                if (Index == Data.Length)
                    throw new EndOfStreamException($"{nameof(WriteBit)} attempted to write past end of stream");

                Index++;
                BitIndex = 8;
            }

            if (bit == 0)
            {
                byte mask = (byte) ~(1 << (BitIndex - 1));
                Data[Index] &= mask;
            }
            else
                Data[Index] |= (byte)(1 << (BitIndex - 1));

            BitsRemaining--;
            BitIndex--;
        }

        public void WriteBits(int val, int numBits)
        {
            throw new NotImplementedException();
        }
    }
}
