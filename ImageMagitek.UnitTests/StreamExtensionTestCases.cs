using System.Collections.Generic;
using NUnit.Framework;

namespace ImageMagitek.UnitTests
{
    public class StreamExtensionTestCases
    {
        public static byte[] readData = new byte[] { 0b10110011, 0b11111111, 0b01010101, 0b11001100, 0b10000001 };

        public static IEnumerable<TestCaseData> ReadUnshiftedCases
        {
            get
            {
                yield return new TestCaseData(readData, new FileBitAddress(0, 0), 1, new byte[] { 0b10000000 });
                yield return new TestCaseData(readData, new FileBitAddress(0, 2), 2, new byte[] { 0b00110000 });
                yield return new TestCaseData(readData, new FileBitAddress(0, 0), 8, new byte[] { 0b10110011 });
                yield return new TestCaseData(readData, new FileBitAddress(0, 0), 9, new byte[] { 0b10110011, 0b10000000 });
                yield return new TestCaseData(readData, new FileBitAddress(1, 1), 9, new byte[] { 0b01111111, 0b01000000 });

                yield return new TestCaseData(readData, new FileBitAddress(4, 0), 8, new byte[] { 0b10000001 });
                yield return new TestCaseData(readData, new FileBitAddress(4, 1), 7, new byte[] { 0b00000001 });
                yield return new TestCaseData(readData, new FileBitAddress(4, 7), 1, new byte[] { 0b00000001 });

                yield return new TestCaseData(readData, new FileBitAddress(0, 1), 38, new byte[] { 0b00110011, 0b11111111, 0b01010101, 0b11001100, 0b10000000 });
                yield return new TestCaseData(readData, new FileBitAddress(0, 0), 40, readData);
            }
        }
    }
}
