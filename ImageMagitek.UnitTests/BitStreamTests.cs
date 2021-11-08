using NUnit.Framework;

namespace ImageMagitek.UnitTests;

[TestFixture]
public class BitStreamTests
{
    [TestCaseSource(typeof(BitStreamTestCases), "ReadBitCases")]
    public void ReadBit_ReturnsExpected(BitStream stream, int bitSeek, int expected)
    {
        stream.SeekAbsolute(bitSeek);
        var actual = stream.ReadBit();

        Assert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(BitStreamTestCases), "ReadBitMultipleCases")]
    public void ReadBit_Multiple_ReturnsExpected(BitStream stream, int[] expected)
    {
        var actual = new int[expected.Length];

        for (int i = 0; i < expected.Length; i++)
            actual[i] = stream.ReadBit();

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(BitStreamTestCases), "ReadByteCases")]
    public void ReadByte_ReturnsExpected(BitStream stream, int bitSeek, byte expected)
    {
        stream.SeekAbsolute(bitSeek);
        var actual = stream.ReadByte();

        Assert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(BitStreamTestCases), "ReadByteMultipleCases")]
    public void ReadByte_Multiple_ReturnsExpected(BitStream stream, int bitSeek, int[] expected)
    {
        stream.SeekAbsolute(bitSeek);
        var actual = new int[expected.Length];

        for (int i = 0; i < expected.Length; i++)
            actual[i] = stream.ReadByte();

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(BitStreamTestCases), "ReadBitsCases")]
    public void ReadBits_ReturnsExpected(BitStream stream, int bitSeek, int bitReadSize, int expected)
    {
        stream.SeekAbsolute(bitSeek);
        var actual = stream.ReadBits(bitReadSize);

        Assert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(BitStreamTestCases), "ReadBitsMultipleCases")]
    public void ReadBits_Multiple_ReturnsExpected(BitStream stream, int[] readSizes, int[] expected)
    {
        var actual = new int[readSizes.Length];

        for (int i = 0; i < readSizes.Length; i++)
            actual[i] = stream.ReadBits(readSizes[i]);

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(BitStreamTestCases), "WriteBitCases")]
    public void WriteBit_AsExpected(BitStream stream, int bitSeek, int writeBit, int expected)
    {
        stream.SeekAbsolute(bitSeek);
        stream.WriteBit(writeBit);
        int actual = (int)stream.Data[bitSeek / 8];

        Assert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(BitStreamTestCases), "WriteBitMultipleCases")]
    public void WriteBit_Multiple_AsExpected(BitStream stream, int bitSeek, int[] writeBits, byte[] expected)
    {
        stream.SeekAbsolute(bitSeek);

        foreach (int bit in writeBits)
            stream.WriteBit((byte)bit);

        var actual = stream.Data;

        Assert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(BitStreamTestCases), "WriteByteCases")]
    public void WriteByte_AsExpected(BitStream stream, int bitSeek, int writeByte, byte[] expected)
    {
        stream.SeekAbsolute(bitSeek);
        stream.WriteByte((byte)writeByte);
        var actual = stream.Data;

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(BitStreamTestCases), "WriteByteMultipleCases")]
    public void WriteByte_Multiple_AsExpected(BitStream stream, int bitSeek, int[] writeBytes, byte[] expected)
    {
        stream.SeekAbsolute(bitSeek);

        foreach (int value in writeBytes)
            stream.WriteByte((byte)value);

        var actual = stream.Data;

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(BitStreamTestCases), "WriteBitsCases")]
    public void WriteBits_AsExpected(BitStream stream, int bitSeek, int writeBits, int bitWriteSize, byte[] expected)
    {
        stream.SeekAbsolute(bitSeek);
        stream.WriteBits(writeBits, bitWriteSize);
        var actual = stream.Data;

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(BitStreamTestCases), "WriteBitsMultipleCases")]
    public void WriteBits_Multiple_AsExpected(BitStream stream, int bitSeek, int[] writeBits, int[] bitWriteSize, byte[] expected)
    {
        stream.SeekAbsolute(bitSeek);

        for (int i = 0; i < writeBits.Length; i++)
            stream.WriteBits(writeBits[i], bitWriteSize[i]);
        var actual = stream.Data;

        CollectionAssert.AreEqual(expected, actual);
    }
}
