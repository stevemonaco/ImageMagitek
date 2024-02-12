using System;
using ImageMagitek.ExtensionMethods;
using Xunit;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;
public partial class ByteArrayExtensionTests
{
    [Theory]
    [MemberData(nameof(ShiftLeftCases))]
    public void ShiftLeft_AsExpected(byte[] array, int count, byte[] expected)
    {
        var span = new Span<byte>(array);
        span.ShiftLeft(count);
        Assert.Equal(expected, array);
    }

    [Theory]
    [MemberData(nameof(ShiftRightCases))]
    public void ShiftRight_AsExpected(byte[] array, int count, byte[] expected)
    {
        var span = new Span<byte>(array);
        span.ShiftRight(count);
        Assert.Equal(expected, array);
    }
}
