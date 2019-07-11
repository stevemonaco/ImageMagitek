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

        [TestCaseSource(typeof(BitStreamTestCases), "ReadByteCases")]
        public void ReadByte_ReturnsExpected(BitStream stream, int bitSeek, byte expected)
        {
            stream.SeekAbsolute(bitSeek);

            var actual = stream.ReadByte();

            Assert.AreEqual(expected, actual);
        }

        [TestCaseSource(typeof(BitStreamTestCases), "ReadBitsCases")]
        public void ReadBits_ReturnsExpected(BitStream stream, int bitSeek, int bitReadSize, int expected)
        {
            stream.SeekAbsolute(bitSeek);

            var actual = stream.ReadBits(bitReadSize);

            Assert.AreEqual(expected, actual);
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
