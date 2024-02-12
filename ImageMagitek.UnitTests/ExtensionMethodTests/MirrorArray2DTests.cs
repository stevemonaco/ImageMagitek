using System.Linq;
using ImageMagitek.ExtensionMethods;
using Xunit;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;
public partial class MirrorArray2DTests
{
    [Theory]
    [MemberData(nameof(MirrorHorizontalCases))]
    public void MirrorArray2D_HorizontalMirroring_AsExpected(byte[,] source, byte[,] expected)
    {
        source.MirrorArray2D(MirrorOperation.Horizontal);
        Assert.Equal(source.Cast(), expected.Cast());
    }

    [Theory]
    [MemberData(nameof(MirrorVerticalCases))]
    public void MirrorArray2D_VerticalMirroring_AsExpected(byte[,] source, byte[,] expected)
    {
        source.MirrorArray2D(MirrorOperation.Vertical);
        Assert.Equal(source.Cast(), expected.Cast());
    }

    [Theory]
    [MemberData(nameof(MirrorBothCases))]
    public void MirrorArray2D_BothMirroring_AsExpected(byte[,] source, byte[,] expected)
    {
        source.MirrorArray2D(MirrorOperation.Both);
        Assert.Equal(source.Cast(), expected.Cast());
    }
}
