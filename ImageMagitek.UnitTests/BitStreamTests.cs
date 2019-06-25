using System;
using System.Collections.Generic;
using ImageMagitek;
using NUnit.Framework;

namespace ImageMagitek.UnitTests
{
    [TestFixture]
    public class BitStreamTests
    {
        [TestCaseSource(typeof(BitStreamTestCases), "ReadBitCases")]
        public void ReadBit_ReturnsValue(BitStream stream, int bitSeek, int expected)
        {
            for (int i = 0; i < bitSeek; i++)
                stream.ReadBit();

            var actual = stream.ReadBit();

            Assert.AreEqual(expected, actual);
        }

        [TestCaseSource(typeof(BitStreamTestCases), "ReadByteCases")]
        public void ReadByte_ReturnsValue(BitStream stream, int bitSeek, byte expected)
        {
            for (int i = 0; i < bitSeek; i++)
                stream.ReadBit();

            var actual = stream.ReadByte();

            Assert.AreEqual(expected, actual);
        }

        [TestCaseSource(typeof(BitStreamTestCases), "ReadBitsCases")]
        public void Readits_ReturnsValue(BitStream stream, int bitSeek, int bitReadSize, int expected)
        {
            for (int i = 0; i < bitSeek; i++)
                stream.ReadBit();

            var actual = stream.ReadBits(bitReadSize);

            Assert.AreEqual(expected, actual);
        }
    }
}
