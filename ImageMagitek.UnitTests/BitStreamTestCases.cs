using NUnit.Framework;
using System.Collections.Generic;

namespace ImageMagitek.UnitTests;

public class BitStreamTestCases
{
    public static byte[] ReadData = new byte[] { 0b10110011, 0b11111111, 0b01010101, 0b11001100, 0b00000001 };
    public const int _readDataLength = 40;
    public static byte[] WriteData => new byte[] { 0b10110011, 0b11111111, 0b01010101, 0b11001100, 0b00000001 };
    public const int _writeDataLength = 40;

    public static IEnumerable<TestCaseData> ReadBitCases
    {
        get
        {
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 0, 1);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 1, 0);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 2, 1);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 7, 1);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 16, 0);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 39, 1);
        }
    }

    public static IEnumerable<TestCaseData> ReadBitMultipleCases
    {
        get
        {
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), new int[] { 1, 0, 1, 1, 0, 0, 1, 1, 1, 1, 1 });
        }
    }

    public static IEnumerable<TestCaseData> ReadByteCases
    {
        get
        {
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 0, (byte)0b10110011);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 1, (byte)0b01100111);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 2, (byte)0b11001111);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 3, (byte)0b10011111);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 7, (byte)0b11111111);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 16, (byte)0b01010101);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 32, (byte)0b00000001);
        }
    }

    public static IEnumerable<TestCaseData> ReadByteMultipleCases
    {
        get
        {
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 3, new int[] { 0b10011111, 0b11111010, 0b10101110, 0b01100000 });
        }
    }

    public static IEnumerable<TestCaseData> ReadBitsCases
    {
        get
        {
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 0, 1, 1);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 0, 2, 2);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 8, 8, 0b11111111);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 10, 8, 0b11111101);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 19, 18, 0b101011100110000000);
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), 16, 24, 0b010101011100110000000001);
        }
    }

    public static IEnumerable<TestCaseData> ReadBitsMultipleCases
    {
        get
        {
            yield return new TestCaseData(BitStream.OpenRead(ReadData, _readDataLength), new int[] { 4, 8, 3, 5 }, new int[] { 0b1011, 0b00111111, 0b111, 0b10101 });
        }
    }

    public static IEnumerable<TestCaseData> WriteBitCases
    {
        get
        {
            yield return new TestCaseData(BitStream.OpenWrite(WriteData, _writeDataLength, 8), 0, 1, 0b10110011);
            yield return new TestCaseData(BitStream.OpenWrite(WriteData, _writeDataLength, 8), 1, 1, 0b11110011);
            yield return new TestCaseData(BitStream.OpenWrite(WriteData, _writeDataLength, 8), 0, 0, 0b00110011);
            yield return new TestCaseData(BitStream.OpenWrite(WriteData, _writeDataLength, 8), 7, 0, 0b10110010);
            yield return new TestCaseData(BitStream.OpenWrite(WriteData, _writeDataLength, 8), 8, 0, 0b01111111);
            yield return new TestCaseData(BitStream.OpenWrite(WriteData, _writeDataLength, 8), 8, 1, 0b11111111);
            yield return new TestCaseData(BitStream.OpenWrite(WriteData, _writeDataLength, 8), 39, 0, 0b00000000);
            yield return new TestCaseData(BitStream.OpenWrite(WriteData, _writeDataLength, 8), 39, 1, 0b00000001);
        }
    }

    public static IEnumerable<TestCaseData> WriteBitMultipleCases
    {
        get
        {
            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 3,
                new int[] { 1, 0, 1, 1, 0, 1, 1, 0, 0, 0, 1 },
                new byte[] { 0b10110110, 0b11000111, 0b01010101, 0b11001100, 0b00000001 });
        }
    }

    public static IEnumerable<TestCaseData> WriteByteCases
    {
        get
        {
            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 0, 0b11111110,
                new byte[] { 0b11111110, 0b11111111, 0b01010101, 0b11001100, 0b00000001 });

            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 4, 0b10101010,
                new byte[] { 0b10111010, 0b10101111, 0b01010101, 0b11001100, 0b00000001 });
        }
    }

    public static IEnumerable<TestCaseData> WriteByteMultipleCases
    {
        get
        {
            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 0,
                new int[] { 0b11001110, 0b00110100, 0b00010110, 0b00101101, 0b00011100 },
                new byte[] { 0b11001110, 0b00110100, 0b00010110, 0b00101101, 0b00011100 });

            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 5,
                new int[] { 0b10101110, 0b10110100, 0b11111111, 0b00101101 },
                new byte[] { 0b10110101, 0b01110101, 0b10100111, 0b11111001, 0b01101001 });
        }
    }

    public static IEnumerable<TestCaseData> WriteBitsCases
    {
        get
        {
            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 0, 0, 1,
                new byte[] { 0b00110011, 0b11111111, 0b01010101, 0b11001100, 0b00000001 });

            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 1, 1, 1,
                new byte[] { 0b11110011, 0b11111111, 0b01010101, 0b11001100, 0b00000001 });

            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 39, 0, 1,
                new byte[] { 0b10110011, 0b11111111, 0b01010101, 0b11001100, 0b00000000 });

            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 0, 0b00011010101, 11,
                new byte[] { 0b00011010, 0b10111111, 0b01010101, 0b11001100, 0b00000001 });

            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 10, 0b00011010101, 11,
                new byte[] { 0b10110011, 0b11000110, 0b10101101, 0b11001100, 0b00000001 });

            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 30, 0b1001101010, 10,
                new byte[] { 0b10110011, 0b11111111, 0b01010101, 0b11001110, 0b01101010 });
        }
    }

    public static IEnumerable<TestCaseData> WriteBitsMultipleCases
    {
        get
        {
            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 0,
                new int[] { 0b10101, 0b10001, 0b100111101100101010 },
                new int[] { 5, 5, 18 },
                new byte[] { 0b10101100, 0b01100111, 0b10110010, 0b10101100, 0b00000001 });

            yield return new TestCaseData(
                BitStream.OpenWrite(WriteData, _writeDataLength, 8), 10,
                new int[] { 0b10101, 0b10001, 0b10011110 },
                new int[] { 5, 5, 8 },
                new byte[] { 0b10110011, 0b11101011, 0b00011001, 0b11101100, 0b00000001 });
        }
    }
}
