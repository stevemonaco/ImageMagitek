using NUnit.Framework;

namespace ImageMagitek.UnitTests
{
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
        public void WriteBit_ReturnsExpected(BitStream stream, int bitSeek, int writeBit, int expected)
        {
            stream.SeekAbsolute(bitSeek);

            stream.WriteBit(writeBit);

            int actual = (int) stream.Data[bitSeek / 8];

            Assert.AreEqual(expected, actual);
        }
    }
}
