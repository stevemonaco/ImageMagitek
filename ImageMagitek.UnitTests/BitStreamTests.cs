using Xunit;

namespace ImageMagitek.UnitTests;
public partial class BitStreamTests
{
    [Theory]
    [MemberData(nameof(ReadBitCases))]
    public void ReadBit_ReturnsExpected(IBitStreamReader stream, int bitSeek, int expected)
    {
        stream.SeekAbsolute(bitSeek);
        var actual = stream.ReadBit();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(ReadBitMultipleCases))]
    public void ReadBit_Multiple_ReturnsExpected(IBitStreamReader stream, int[] expected)
    {
        var actual = new int[expected.Length];

        for (int i = 0; i < expected.Length; i++)
            actual[i] = stream.ReadBit();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(ReadByteCases))]
    public void ReadByte_ReturnsExpected(IBitStreamReader stream, int bitSeek, byte expected)
    {
        stream.SeekAbsolute(bitSeek);
        var actual = stream.ReadByte();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(ReadByteMultipleCases))]
    public void ReadByte_Multiple_ReturnsExpected(IBitStreamReader stream, int bitSeek, int[] expected)
    {
        stream.SeekAbsolute(bitSeek);
        var actual = new int[expected.Length];

        for (int i = 0; i < expected.Length; i++)
            actual[i] = stream.ReadByte();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(ReadBitsCases))]
    public void ReadBits_ReturnsExpected(IBitStreamReader stream, int bitSeek, int bitReadSize, int expected)
    {
        stream.SeekAbsolute(bitSeek);
        var actual = stream.ReadBits(bitReadSize);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(ReadBitsMultipleCases))]
    public void ReadBits_Multiple_ReturnsExpected(IBitStreamReader stream, int[] readSizes, int[] expected)
    {
        var actual = new int[readSizes.Length];

        for (int i = 0; i < readSizes.Length; i++)
            actual[i] = stream.ReadBits(readSizes[i]);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(WriteBitCases))]
    public void WriteBit_AsExpected(IBitStreamWriter stream, int bitSeek, int writeBit, int expected)
    {
        stream.SeekAbsolute(bitSeek);
        stream.WriteBit(writeBit);
        int actual = (int)stream.Data[bitSeek / 8];

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(WriteBitMultipleCases))]
    public void WriteBit_Multiple_AsExpected(IBitStreamWriter stream, int bitSeek, int[] writeBits, byte[] expected)
    {
        stream.SeekAbsolute(bitSeek);

        foreach (int bit in writeBits)
            stream.WriteBit((byte)bit);

        var actual = stream.Data;

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(WriteByteCases))]
    public void WriteByte_AsExpected(IBitStreamWriter stream, int bitSeek, int writeByte, byte[] expected)
    {
        stream.SeekAbsolute(bitSeek);
        stream.WriteByte((byte)writeByte);
        var actual = stream.Data;

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(WriteByteMultipleCases))]
    public void WriteByte_Multiple_AsExpected(IBitStreamWriter stream, int bitSeek, int[] writeBytes, byte[] expected)
    {
        stream.SeekAbsolute(bitSeek);

        foreach (int value in writeBytes)
            stream.WriteByte((byte)value);

        var actual = stream.Data;

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(WriteBitsCases))]
    public void WriteBits_AsExpected(IBitStreamWriter stream, int bitSeek, int writeBits, int bitWriteSize, byte[] expected)
    {
        stream.SeekAbsolute(bitSeek);
        stream.WriteBits(writeBits, bitWriteSize);
        var actual = stream.Data;

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(WriteBitsMultipleCases))]
    public void WriteBits_Multiple_AsExpected(IBitStreamWriter stream, int bitSeek, int[] writeBits, int[] bitWriteSize, byte[] expected)
    {
        stream.SeekAbsolute(bitSeek);

        for (int i = 0; i < writeBits.Length; i++)
            stream.WriteBits(writeBits[i], bitWriteSize[i]);
        var actual = stream.Data;

        Assert.Equal(expected, actual);
    }
}
