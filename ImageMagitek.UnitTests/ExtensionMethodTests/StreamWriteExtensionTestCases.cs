using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.UnitTests.ExtensionMethodTests
{
    public class StreamWriteExtensionTestCases
    {
        public static byte[] singleArray = new byte[] { 0b10011101 };
        public static byte[] largeArray = new byte[] { 0b10110011, 0b11111111, 0b01010101, 0b11001100, 0b10000001 };

        public static IEnumerable<TestCaseData> WriteUnshiftedCases
        {
            get
            {
                yield return new TestCaseData(singleArray.Clone(), new FileBitAddress(0, 0), 1, 
                    new byte[] { 0b00000000 }, new byte[] { 0b00011101 });
                yield return new TestCaseData(singleArray.Clone(), new FileBitAddress(0, 2), 2, 
                    new byte[] { 0b00110000 }, new byte[] { 0b10111101 });
                yield return new TestCaseData(singleArray.Clone(), new FileBitAddress(0, 2), 6, 
                    new byte[] { 0b00111110 }, new byte[] { 0b10111110 });

                yield return new TestCaseData(largeArray.Clone(), new FileBitAddress(1, 3), 14, 
                    new byte[] { 0b00010101, 0b11010100, 0b00000000 }, new byte[] { 0b10110011, 0b11110101, 0b11010100, 0b01001100, 0b10000001 });

                yield return new TestCaseData(largeArray.Clone(), new FileBitAddress(0, 1), 38,
                    new byte[] { 0b00010101, 0b11010100, 0b01111111, 0b01010100, 0b01101010 }, new byte[] { 0b10010101, 0b11010100, 0b01111111, 0b01010100, 0b01101011 });

                yield return new TestCaseData(largeArray.Clone(), new FileBitAddress(0, 0), 40,
                    new byte[] { 0b00010101, 0b11010100, 0b01111111, 0b01010100, 0b01101010 }, new byte[] { 0b00010101, 0b11010100, 0b01111111, 0b01010100, 0b01101010 });

                // Cases with unmasked bits outside of writing range
                yield return new TestCaseData(singleArray.Clone(), new FileBitAddress(0, 0), 1,
                    new byte[] { 0b00000010 }, new byte[] { 0b00011101 });

                yield return new TestCaseData(largeArray.Clone(), new FileBitAddress(1, 3), 14,
                    new byte[] { 0b00010101, 0b11010100, 0b01111111 }, new byte[] { 0b10110011, 0b11110101, 0b11010100, 0b01001100, 0b10000001 });
            }
        }

        //public static byte[] singleArray = new byte[] { 0b10011101 };
        //public static byte[] largeArray = new byte[] { 0b10110011, 0b11111111, 0b01010101, 0b11001100, 0b10000001 };

        public static IEnumerable<TestCaseData> WriteShiftedCases
        {
            get
            {
                yield return new TestCaseData(singleArray.Clone(), new FileBitAddress(0, 0), 1,
                    new byte[] { 0b00000000 }, new byte[] { 0b00011101 });
                yield return new TestCaseData(singleArray.Clone(), new FileBitAddress(0, 2), 2,
                    new byte[] { 0b11000000 }, new byte[] { 0b10111101 });
                yield return new TestCaseData(singleArray.Clone(), new FileBitAddress(0, 2), 6,
                    new byte[] { 0b11111000 }, new byte[] { 0b10111110 });

                yield return new TestCaseData(largeArray.Clone(), new FileBitAddress(1, 3), 14,
                    new byte[] { 0b10101110, 0b10100000, 0b00000000 }, new byte[] { 0b10110011, 0b11110101, 0b11010100, 0b01001100, 0b10000001 });

                yield return new TestCaseData(largeArray.Clone(), new FileBitAddress(0, 1), 38,
                    new byte[] { 0b00101011, 0b10101000, 0b11111110, 0b10101000, 0b11010100 }, new byte[] { 0b10010101, 0b11010100, 0b01111111, 0b01010100, 0b01101011 });

                yield return new TestCaseData(largeArray.Clone(), new FileBitAddress(0, 0), 40,
                    new byte[] { 0b00010101, 0b11010100, 0b01111111, 0b01010100, 0b01101010 }, new byte[] { 0b00010101, 0b11010100, 0b01111111, 0b01010100, 0b01101010 });

                // Cases with unmasked bits outside of writing range
                yield return new TestCaseData(singleArray.Clone(), new FileBitAddress(0, 0), 1,
                    new byte[] { 0b00000010 }, new byte[] { 0b00011101 });

                yield return new TestCaseData(largeArray.Clone(), new FileBitAddress(1, 3), 14,
                    new byte[] { 0b10101110, 0b10100011, 0b11111000 }, new byte[] { 0b10110011, 0b11110101, 0b11010100, 0b01001100, 0b10000001 });
            }
        }
    }
}
