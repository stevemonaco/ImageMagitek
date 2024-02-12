using System.IO;
using ImageMagitek.ExtensionMethods;
using Xunit;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;
public partial class StreamReadExtensionTests
{
    [Theory]
    [MemberData(nameof(ReadUnshiftedCases))]
    public void ReadUnshifted_AsExpected(byte[] data, BitAddress offset, int numBits, byte[] expected)
    {
        var stream = new MemoryStream(data);
        var actual = new byte[(numBits + 7) / 8];
        stream.ReadUnshifted(offset, numBits, actual);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(ReadShiftedCases))]
    public void ReadShifted_AsExpected(byte[] data, BitAddress offset, int numBits, byte[] expected)
    {
        var stream = new MemoryStream(data);
        var actual = new byte[(numBits + 7) / 8];
        stream.ReadShifted(offset, numBits, actual);

        Assert.Equal(expected, actual);
    }
}
