using System.IO;
using ImageMagitek.ExtensionMethods;
using Xunit;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;
public partial class StreamWriteExtensionTests
{
    [Theory]
    [MemberData(nameof(WriteUnshiftedCases))]
    public void WriteUnshifted_AsExpected(byte[] data, BitAddress offset, int numBits, byte[] writeData, byte[] expected)
    {
        using var stream = new MemoryStream(data);
        stream.WriteUnshifted(offset, numBits, writeData);

        stream.Seek(0, SeekOrigin.Begin);
        var actual = new byte[expected.Length];
        stream.Read(actual);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(WriteShiftedCases))]
    public void WriteShifted_AsExpected(byte[] data, BitAddress offset, int numBits, byte[] writeData, byte[] expected)
    {
        using var stream = new MemoryStream(data);
        stream.WriteShifted(offset, numBits, writeData);

        stream.Seek(0, SeekOrigin.Begin);
        var actual = new byte[expected.Length];
        stream.Read(actual);

        Assert.Equal(expected, actual);
    }
}
