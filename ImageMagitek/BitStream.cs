using System;
using System.IO;
using System.Text;

namespace ImageMagitek;

public interface IBitStream
{
    byte[] Data { get; }
    void SeekAbsolute(int seekBits);
    void SeekRelative(int seekBits);
}

public interface IBitStreamReader : IBitStream
{
    int ReadBit();
    int ReadBits(int numBits);
    byte ReadByte();
}

public interface IBitStreamWriter : IBitStream
{
    void WriteBit(int bit);
    void WriteByte(byte val);
    void WriteBits(int val, int numBits);

}

/// <summary>
/// Stream class with specific features for bit reading and writing of arrays
/// </summary>
public sealed class BitStream : IBitStreamReader, IBitStreamWriter
{
    private enum BitStreamAccess { Read, Write, ReadWrite };

    /// <summary>
    /// Current working bit index
    /// </summary>
    private int _bitIndex;

    /// <summary>
    ///  Current working index into data array where bits are read/written to
    /// </summary>
    private int _index;

    /// <summary>
    /// Bits remaining in the stream
    /// </summary>
    private int _bitsRemaining;

    /// <summary>
    /// Type of access to the stream
    /// </summary>
    private BitStreamAccess _access;

    private int _streamStartOffset;

    private int _streamEndOffset;

    private int StreamSize { get => _streamEndOffset - _streamStartOffset; }

    public byte[] Data { get; }

    private BitStream(byte[] data)
    {
        Data = data;
    }

    /// <summary>
    /// Creates a new Bitstream to read bits from the specified array
    /// </summary>
    /// <param name="readData">Data to be wrapped for reading</param>
    /// <param name="dataBits">Number of valid bits to read in the array</param>
    /// <returns></returns>
    public static IBitStreamReader OpenRead(byte[] readData, int dataBits)
    {
        return new BitStream(readData)
        {
            _bitsRemaining = dataBits,
            _bitIndex = 8,
            _index = 0,
            _access = BitStreamAccess.Read,
            _streamStartOffset = 0,
            _streamEndOffset = dataBits
        };
    }

    /// <summary>
    /// Creates a new BitStream with its own array from a BinaryReader for bit reading
    /// </summary>
    /// <param name="br">Underlying binary reader for the stream</param>
    /// <param name="dataBits"></param>
    /// <param name="firstByteBits"></param>
    /// <returns>A readable BitStream instance</returns>
    public static IBitStreamReader OpenRead(BinaryReader br, int dataBits, int firstByteBits)
    {
        int ReadLength = (int)Math.Ceiling((dataBits + (8 - firstByteBits)) / 8.0);
        var data = br.ReadBytes(ReadLength);
        byte mask = (byte)((1 << firstByteBits) - 1);
        data[0] = (byte)(data[0] & mask);

        return new BitStream(data)
        {
            _bitIndex = firstByteBits,
            _bitsRemaining = dataBits,
            _index = 0,
            _access = BitStreamAccess.Read,
            _streamStartOffset = 8 - firstByteBits,
            _streamEndOffset = dataBits - (8 - firstByteBits),
        };
    }

    /// <summary>
    /// Creates a new BitStream with its own array from a Stream for bit reading
    /// </summary>
    /// <param name="br">Underlying stream</param>
    /// <param name="dataBits"></param>
    /// <param name="firstByteBits"></param>
    /// <returns>A readable BitStream instance</returns>
    public static IBitStreamReader OpenRead(Stream stream, int dataBits, int firstByteBits)
    {
        var br = new BinaryReader(stream, Encoding.Default, true);
        return OpenRead(br, dataBits, firstByteBits);
    }

    /// <summary>
    /// Creates a new BitStream for writing bits to an array
    /// </summary>
    /// <param name="dataBits">Size of writable array in bits</param>
    /// <param name="firstByteBits">Number of bits available for writing in the first byte</param>
    /// <returns>A writable BitStream instance</returns>
    public static IBitStreamWriter OpenWrite(int dataBits, int firstByteBits)
    {
        int BufferLength = (int)Math.Ceiling((dataBits + (8 - firstByteBits)) / 8.0);
        var data = new byte[BufferLength];

        return OpenWrite(data, dataBits, firstByteBits);
    }

    public static IBitStreamWriter OpenWrite(byte[] Buffer, int dataBits, int firstByteBits)
    {
        return new BitStream(Buffer)
        {
            _bitIndex = firstByteBits,
            _bitsRemaining = dataBits,
            _index = 0,
            _access = BitStreamAccess.Write,
            _streamStartOffset = 8 - firstByteBits,
            _streamEndOffset = dataBits - (8 - firstByteBits),
        };
    }

    public void SeekAbsolute(int seekBits)
    {
        if (seekBits < 0 || seekBits >= StreamSize)
            throw new ArgumentOutOfRangeException($"{nameof(SeekAbsolute)} parameter '{nameof(seekBits)} is out of range ({seekBits})'");

        _index = (_streamStartOffset + seekBits) / 8;
        _bitIndex = 8 - (_streamStartOffset + seekBits) % 8;
        _bitsRemaining = _streamEndOffset - (_streamStartOffset + seekBits);
    }

    public void SeekRelative(int seekBits)
    {
        int seekOffset = _index * 8 + (8 - _bitIndex) + seekBits;

        if (seekOffset < 0 || seekOffset >= StreamSize)
            throw new ArgumentOutOfRangeException($"{nameof(SeekRelative)} parameter '{nameof(seekOffset)} is out of range ({seekOffset})'");

        _index = (_streamStartOffset + seekOffset) / 8;
        _bitIndex = 8 - (_streamStartOffset + seekOffset) % 8;
        _bitsRemaining = _streamEndOffset - (_streamStartOffset + seekOffset);
    }

    /// <summary>
    /// Reads a single bit from the underlying stream
    /// </summary>
    /// <returns></returns>
    public int ReadBit()
    {
        if (_access != BitStreamAccess.Read && _access != BitStreamAccess.ReadWrite)
            throw new InvalidOperationException($"{nameof(ReadBit)} does not have read access");
        if (_bitsRemaining == 0)
            throw new EndOfStreamException($"{nameof(ReadBit)} read past end of stream");

        if (_bitIndex == 0)
        {
            _index++;
            if (_index == Data.Length)
                throw new EndOfStreamException($"{nameof(ReadBit)} read past end of stream");

            _bitIndex = 8;
        }

        int bit = (Data[_index] >> (_bitIndex - 1)) & 1;
        _bitsRemaining--;
        _bitIndex--;

        return bit;
    }

    /// <summary>
    /// Reads a single byte from the underlying stream
    /// </summary>
    /// <returns></returns>
    public byte ReadByte()
    {
        if (_bitsRemaining < 8)
            throw new EndOfStreamException($"{nameof(ReadByte)} read past end of stream");

        byte result = 0;

        if (_bitIndex == 8)
        {
            result = Data[_index];
            Advance(8);
        }
        else
        {
            int readSize = _bitIndex;
            result = (byte)(PartialRead(readSize) << (8 - readSize));
            readSize = 8 - readSize;
            result = (byte)(result | PartialRead(readSize));
        }

        return result;
    }

    /// <summary>
    /// Reads the specified number of bits from the stream
    /// </summary>
    /// <param name="bitReadLength">Number of bits to read between 1 and 32</param>
    /// <returns></returns>
    public int ReadBits(int bitReadLength)
    {
        if (bitReadLength > 32 || bitReadLength < 1)
            throw new ArgumentOutOfRangeException($"{nameof(ReadBits)} parameter {nameof(bitReadLength)} ({bitReadLength}) is out of range");

        if (bitReadLength > _bitsRemaining)
            throw new EndOfStreamException($"{nameof(ReadBits)} read past end of stream");

        var readRemaining = bitReadLength; // Number of bits remaining to be read
        int result = 0;

        // Unaligned, partial read
        if (_bitIndex != 8)
        {
            int readLength = Math.Min(_bitIndex, readRemaining);
            result = PartialRead(readLength);
            readRemaining -= readLength;
        }

        // Multiple aligned byte reads
        while (readRemaining >= 8)
        {
            result = (result << 8) | Data[_index];
            Advance(8);
            readRemaining -= 8;
        }

        // Final unaligned read
        if (readRemaining > 0)
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
        mask <<= (_bitIndex - bitReadLength); // Shift mask to the bit index

        int result = (Data[_index] & mask) >> (_bitIndex - bitReadLength);

        Advance(bitReadLength);

        return result;
    }

    /// <summary>
    /// Advances the stream's position internals
    /// </summary>
    /// <param name="advanceLength">Number of bits to advance</param>
    private void Advance(int advanceLength)
    {
        int offset = (8 - _bitIndex) + advanceLength;
        _index += offset / 8;
        _bitIndex = 8 - (offset % 8);
        _bitsRemaining -= advanceLength;
    }

    public void WriteBit(int bit)
    {
        if (bit > 1)
            throw new ArgumentOutOfRangeException();
        if (_access != BitStreamAccess.Write && _access != BitStreamAccess.ReadWrite)
            throw new InvalidOperationException($"{nameof(WriteBit)} does not have write access");
        if (_bitsRemaining == 0)
            throw new EndOfStreamException($"{nameof(WriteBit)} attempted to write past end of stream");

        if (_bitIndex == 0)
        {
            if (_index == Data.Length)
                throw new EndOfStreamException($"{nameof(WriteBit)} attempted to write past end of stream");

            _index++;
            _bitIndex = 8;
        }

        if (bit == 0)
        {
            byte mask = (byte)~(1 << (_bitIndex - 1));
            Data[_index] &= mask;
        }
        else
            Data[_index] |= (byte)(1 << (_bitIndex - 1));

        _bitsRemaining--;
        _bitIndex--;
    }

    public void WriteByte(byte val)
    {
        if (_bitsRemaining < 8)
            throw new EndOfStreamException($"{nameof(WriteByte)} read past end of stream");

        if (_bitIndex == 8)
        {
            Data[_index] = val;
            Advance(8);
        }
        else
        {
            int writeSize = _bitIndex;
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

        if (numBits > _bitsRemaining)
            throw new EndOfStreamException($"{nameof(WriteBits)} read past end of stream");

        var writeRemaining = numBits;

        // Unaligned, partial write
        if (_bitIndex != 8)
        {
            int writeLength = Math.Min(_bitIndex, writeRemaining);
            int writeValue = val >> (writeRemaining - writeLength);
            PartialWrite(writeValue, writeLength);
            writeRemaining -= writeLength;
        }

        // Multiple aligned byte reads
        while (writeRemaining >= 8)
        {
            int writeValue = val >> (writeRemaining - 8);
            writeValue &= (1 << 8) - 1;
            Data[_index] = (byte)writeValue;
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
        mask <<= (_bitIndex - bitWriteLength); // Shift mask to the bit index

        Data[_index] &= (byte)~mask; // Clear bits
        Data[_index] |= (byte)(val << (_bitIndex - bitWriteLength));

        Advance(bitWriteLength);
    }
}
