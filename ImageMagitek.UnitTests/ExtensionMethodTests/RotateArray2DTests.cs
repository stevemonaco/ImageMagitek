using ImageMagitek.ExtensionMethods;
using System.Linq;
using Xunit;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;
public partial class RotateArray2DTests
{
    [Theory]
    [MemberData(nameof(RotateLeftCases))]
    public void RotateArray2D_Left_AsExpected(byte[,] source, byte[,] expected)
    {
        source.RotateArray2D(RotationOperation.Left);
        Assert.Equal(source.Cast(), expected.Cast());
    }

    [Theory]
    [MemberData(nameof(RotateRightCases))]
    public void RotateArray2D_Right_AsExpected(byte[,] source, byte[,] expected)
    {
        source.RotateArray2D(RotationOperation.Right);
        Assert.Equal(source.Cast(), expected.Cast());
    }

    [Theory]
    [MemberData(nameof(RotateTurnCases))]
    public void RotateArray2D_Turn_AsExpected(byte[,] source, byte[,] expected)
    {
        source.RotateArray2D(RotationOperation.Turn);
        Assert.Equal(source.Cast(), expected.Cast());
    }
}
