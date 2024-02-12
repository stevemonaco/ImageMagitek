using ImageMagitek.ExtensionMethods;
using Xunit;
using System.Linq;

namespace ImageMagitek.UnitTests.ExtensionMethodTests;
public partial class TransposeArray2DTests
{
    [Theory]
    [MemberData(nameof(TransposeCases))]
    public void TransposeArray2D_AsExpected(byte[,] source, byte[,] expected)
    {
        source.TransposeArray2D();
        Assert.Equal(source.Cast(), expected.Cast());
    }
}
