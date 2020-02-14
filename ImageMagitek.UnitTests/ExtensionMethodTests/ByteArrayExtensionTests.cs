using System;
using NUnit.Framework;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.UnitTests
{
    [TestFixture]
    public class ByteArrayExtensionTests
    {
        [TestCaseSource(typeof(ByteArrayExtensionTestCases), "ShiftLeftCases")]
        public void ShiftLeft_AsExpected(byte[] array, int count, byte[] expected)
        {
            var span = new Span<byte>(array);
            span.ShiftLeft(count);
            CollectionAssert.AreEqual(expected, array);
        }

        [TestCaseSource(typeof(ByteArrayExtensionTestCases), "ShiftRightCases")]
        public void ShiftRight_AsExpected(byte[] array, int count, byte[] expected)
        {
            var span = new Span<byte>(array);
            span.ShiftRight(count);
            CollectionAssert.AreEqual(expected, array);
        }
    }
}
