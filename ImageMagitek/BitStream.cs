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
            int seekOffset = Index * 8 + (8 - BitIndex) + seekBits;

            if (seekOffset < 0 || seekOffset >= StreamSize)
                throw new ArgumentOutOfRangeException($"{nameof(SeekRelative)} parameter '{nameof(seekOffset)} is out of range ({seekOffset})'");

            Index = (StreamStartOffset + seekOffset) / 8;
            BitIndex = 8 - (StreamStartOffset + seekOffset) % 8;
            BitsRemaining = StreamEndOffset - (StreamStartOffset + seekOffset);
        }

        /// <summary>
        /// Reads a single bit from the underlying stream
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

        /// <summary>
        /// Reads a single byte from the underlying stream
        /// </summary>
        /// <returns></returns>
        public byte ReadByte()
        {
            if (BitsRemaining < 8)
                throw new EndOfStreamException($"{nameof(ReadByte)} read past end of stream");

            byte result = 0;

            if (BitIndex == 8)
            {
                result = Data[Index];
                Advance(8);
            }
            else
            {
                int readSize = BitIndex;
                result = (byte)(PartialRead(readSize) << (8 - readSize));
                readSize = 8 - readSize;
                result = (byte)(result | PartialRead(readSize));
            }

            return result;
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

            var readRemaining = numBits; // Number of bits remaining to be read
            int result = 0;

            // Unaligned, partial read
            if (BitIndex != 8)
            {
                int readLength = Math.Min(BitIndex, readRemaining);
                result = PartialRead(readLength);
                readRemaining -= readLength;
            }

            // Multiple aligned byte reads
            while(readRemaining >= 8)
            {
                result = (result << 8) | Data[Index];
                Advance(8);
                readRemaining -= 8;
            }

            // Final unaligned read
            if(readRemaining > 0)
                result = (result << readRemaining) | PartialRead(readRemaining);

            return result;
        }

        /// <summary>
        /// Perform a simple bit read that does not cross byte boundaries
        /// </summary>
        /// <param name="bitReadLength"></param>
        /// <returns></returns>
        private int PartialRead(int bitReadLength)
        {
            int mask = ((1 << bitReadLength) - 1); // Make mask for the bits to be read
            mask <<= (BitIndex - bitReadLength); // Shift mask to the bit index

            int result = (Data[Index] & mask) >> (BitIndex - bitReadLength);

            Advance(bitReadLength);

            return result;
        }

        /// <summary>
        /// Advances the stream's position internals
        /// </summary>
        /// <param name="advanceLength">Number of bits to advance</param>
        private void Advance(int advanceLength)
        {
            int offset = (8 - BitIndex) + advanceLength;
            Index += offset / 8;
            BitIndex = 8 - (offset % 8);
            BitsRemaining -= advanceLength;
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

        public void WriteByte(byte val)
        {
            if (BitsRemaining < 8)
                throw new EndOfStreamException($"{nameof(WriteByte)} read past end of stream");

            if (BitIndex == 8)
            {
                Data[Index] = val;
                Advance(8);
            }
            else
            {
                int writeSize = BitIndex;
                int writeValue = val >> (8 - writeSize);
                PartialWrite(writeValue, writeSize);
                writeSize = 8 - writeSize;
                writeValue = val & ((1 << writeSize) - 1);
                PartialWrite(writeValue, writeSize);
            }
        }

        public void WriteBits(int val, int numBits)
        {
            if (numBits > 32 || numBits < 1)
                throw new ArgumentOutOfRangeException($"{nameof(WriteBits)} parameter {nameof(numBits)} ({numBits}) is out of range");

            if (numBits > BitsRemaining)
                throw new EndOfStreamException($"{nameof(WriteBits)} read past end of stream");

            var writeRemaining = numBits;

            // Unaligned, partial write
            if (BitIndex != 8)
            {
                int writeLength = Math.Min(BitIndex, writeRemaining);
                int writeValue = val >> (writeRemaining - writeLength);
                PartialWrite(writeValue, writeLength);
                writeRemaining -= writeLength;
            }

            // Multiple aligned byte reads
            while (writeRemaining >= 8)
            {
                int writeValue = val >> (writeRemaining - 8);
                writeValue &= (1 << 8) - 1;
                Data[Index] = (byte) writeValue;
                Advance(8);
                writeRemaining -= 8;
            }

            // Final unaligned read
            if (writeRemaining > 0)
            {
                int writeValue = val & ((1 << writeRemaining) - 1);
                PartialWrite(writeValue, writeRemaining);
            }
        }

        private void PartialWrite(int val, int bitWriteLength)
        {
            int mask = ((1 << bitWriteLength) - 1); // Make mask for the bits to be read
            mask <<= (BitIndex - bitWriteLength); // Shift mask to the bit index

            Data[Index] &= (byte) ~mask; // Clear bits
            Data[Index] |= (byte) (val << (BitIndex - bitWriteLength));

            Advance(bitWriteLength);
        }
    }
}
