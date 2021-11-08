using NUnit.Framework;
using System.IO;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;

[TestFixture]
public class StreamWriteExtensionTests
{
    [TestCaseSource(typeof(StreamWriteExtensionTestCases), "WriteUnshiftedCases")]
    public void WriteUnshifted_AsExpected(byte[] data, FileBitAddress offset, int numBits, byte[] writeData, byte[] expected)
    {
        using var stream = new MemoryStream(data);
        stream.WriteUnshifted(offset, numBits, writeData);

        stream.Seek(0, SeekOrigin.Begin);
        var actual = new byte[expected.Length];
        stream.Read(actual);

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestCaseSource(typeof(StreamWriteExtensionTestCases), "WriteShiftedCases")]
    public void WriteShifted_AsExpected(byte[] data, FileBitAddress offset, int numBits, byte[] writeData, byte[] expected)
    {
        using var stream = new MemoryStream(data);
        stream.WriteShifted(offset, numBits, writeData);

        stream.Seek(0, SeekOrigin.Begin);
        var actual = new byte[expected.Length];
        stream.Read(actual);

        CollectionAssert.AreEqual(expected, actual);
    }
}
