using ImageMagitek.Colors;
using Xunit;

namespace ImageMagitek.UnitTests;
public partial class ForeignColorTests
{
    [Theory]
    [MemberData(nameof(ToNativeTestCases))]
    public void ToNative_AsExpected(IColor32 fc, ColorRgba32 expected)
    {
        var colorFactory = new ColorFactory();
        var actual = colorFactory.ToNative(fc);

        Assert.Multiple(() =>
        {
            Assert.Equal(expected.Color, actual.Color);
            Assert.Equal(expected.R, actual.R);
            Assert.Equal(expected.G, actual.G);
            Assert.Equal(expected.B, actual.B);
            Assert.Equal(expected.A, actual.A);
        });
    }
}
