using NUnit.Framework;
using System.Collections.Generic;

namespace ImageMagitek.UnitTests
{
    public class BitStreamTestCases
    {
        public static byte[] readData = new byte[] { 0b10110011, 0b11111111, 0b01010101, 0b11001100, 0b00000001 };
        public static byte[] writeData => new byte[] { 0b10110011, 0b11111111, 0b01010101, 0b11001100, 0b00000001 };

        public static IEnumerable<TestCaseData> ReadBitCases
        {
            get
            {
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 0, 1);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 1, 0);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 2, 1);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 7, 1);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 16, 0);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 39, 1);
            }
        }

        public static IEnumerable<TestCaseData> ReadByteCases
        {
            get
            {
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 0, (byte)0b10110011);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 1, (byte)0b01100111);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 2, (byte)0b11001111);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 3, (byte)0b10011111);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 7, (byte)0b11111111);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 16, (byte)0b01010101);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 32, (byte)0b00000001);
            }
        }

        public static IEnumerable<TestCaseData> ReadBitsCases
        {
            get
            {
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 0, 1, 1);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 0, 2, 2);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 8, 8, 0b11111111);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 10, 8, 0b11111101);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 19, 18, 0b101011100110000000);
                yield return new TestCaseData(BitStream.OpenRead(readData, readData.Length * 8), 16, 24, 0b010101011100110000000001);
            }
        }

        public static IEnumerable<TestCaseData> WriteBitCases
        {
            get
            {
                yield return new TestCaseData(BitStream.OpenWrite(writeData, writeData.Length * 8, 8), 0, 1, 0b10110011);
                yield return new TestCaseData(BitStream.OpenWrite(writeData, writeData.Length * 8, 8), 1, 1, 0b11110011);
                yield return new TestCaseData(BitStream.OpenWrite(writeData, writeData.Length * 8, 8), 0, 0, 0b00110011);
                yield return new TestCaseData(BitStream.OpenWrite(writeData, writeData.Length * 8, 8), 7, 0, 0b10110010);
                yield return new TestCaseData(BitStream.OpenWrite(writeData, writeData.Length * 8, 8), 8, 0, 0b01111111);
                yield return new TestCaseData(BitStream.OpenWrite(writeData, writeData.Length * 8, 8), 8, 1, 0b11111111);
                yield return new TestCaseData(BitStream.OpenWrite(writeData, writeData.Length * 8, 8), 39, 0, 0b00000000);
                yield return new TestCaseData(BitStream.OpenWrite(writeData, writeData.Length * 8, 8), 39, 1, 0b00000001);
            }
        }
    }
}
