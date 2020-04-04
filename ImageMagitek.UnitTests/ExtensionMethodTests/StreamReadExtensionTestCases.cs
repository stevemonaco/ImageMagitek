using System.Collections.Generic;
using NUnit.Framework;

namespace ImageMagitek.UnitTests
{
    public class StreamReadExtensionTestCases
    {
        public static byte[] singleArray = new byte[] { 0b10011101 };
        public static byte[] largeArray = new byte[] { 0b10110011, 0b11111111, 0b01010101, 0b11001100, 0b10000001 };

        public static IEnumerable<TestCaseData> ReadUnshiftedCases
        {
            get
            {
                yield return new TestCaseData(largeArray, new FileBitAddress(0, 0), 1, new byte[] { 0b10000000 });
                yield return new TestCaseData(largeArray, new FileBitAddress(0, 2), 2, new byte[] { 0b00110000 });
                yield return new TestCaseData(largeArray, new FileBitAddress(0, 4), 4, new byte[] { 0b00000011 });
                yield return new TestCaseData(largeArray, new FileBitAddress(0, 0), 8, new byte[] { 0b10110011 });
                yield return new TestCaseData(largeArray, new FileBitAddress(0, 0), 9, new byte[] { 0b10110011, 0b10000000 });
                yield return new TestCaseData(largeArray, new FileBitAddress(1, 1), 9, new byte[] { 0b01111111, 0b01000000 });

                yield return new TestCaseData(largeArray, new FileBitAddress(4, 0), 8, new byte[] { 0b10000001 });
                yield return new TestCaseData(largeArray, new FileBitAddress(4, 1), 7, new byte[] { 0b00000001 });
                yield return new TestCaseData(largeArray, new FileBitAddress(4, 7), 1, new byte[] { 0b00000001 });

                yield return new TestCaseData(largeArray, new FileBitAddress(0, 1), 38, new byte[] { 0b00110011, 0b11111111, 0b01010101, 0b11001100, 0b10000000 });
                yield return new TestCaseData(largeArray, new FileBitAddress(0, 0), 40, largeArray);
            }
        }

        public static IEnumerable<TestCaseData> ReadShiftedCases
        {
            get
            {
                yield return new TestCaseData(singleArray, new FileBitAddress(0, 0), 1, new byte[] { 0b10000000 });
                yield return new TestCaseData(singleArray, new FileBitAddress(0, 2), 2, new byte[] { 0b01000000 });
                yield return new TestCaseData(singleArray, new FileBitAddress(0, 4), 4, new byte[] { 0b11010000 });
                yield return new TestCaseData(singleArray, new FileBitAddress(0, 0), 8, new byte[] { 0b10011101 });

                yield return new TestCaseData(largeArray, new FileBitAddress(0, 0), 9, new byte[] { 0b10110011, 0b10000000 });
                yield return new TestCaseData(largeArray, new FileBitAddress(1, 1), 9, new byte[] { 0b11111110, 0b10000000 });

                yield return new TestCaseData(largeArray, new FileBitAddress(4, 0), 8, new byte[] { 0b10000001 });
                yield return new TestCaseData(largeArray, new FileBitAddress(4, 1), 7, new byte[] { 0b00000010 });
                yield return new TestCaseData(largeArray, new FileBitAddress(3, 1), 8, new byte[] { 0b10011001 });
                yield return new TestCaseData(largeArray, new FileBitAddress(4, 7), 1, new byte[] { 0b10000000 });

                yield return new TestCaseData(largeArray, new FileBitAddress(2, 1), 14, new byte[] { 0b10101011, 0b10011000 });
                yield return new TestCaseData(largeArray, new FileBitAddress(0, 1), 38, new byte[] { 0b01100111, 0b11111110, 0b10101011, 0b10011001, 0b00000010 });
                yield return new TestCaseData(largeArray, new FileBitAddress(0, 0), 40, largeArray);
            }
        }
    }
}
