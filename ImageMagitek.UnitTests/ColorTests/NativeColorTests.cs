using ImageMagitek.Colors;
using Xunit;

namespace ImageMagitek.UnitTests;
public partial class NativeColorTests
{
    [Theory]
    [MemberData(nameof(ToForeignTestCases))]
    public void ToForeignColor_Converts_Correctly(ColorRgba32 nc, IColor32 expected, ColorModel colorModel)
    {
        var colorFactory = new ColorFactory();
        var actual = colorFactory.ToForeign(nc, colorModel);

        Assert.True(actual is IColor32);

        if (actual is IColor32 actual32)
        {
            Assert.Multiple(() =>
            {
                Assert.Equal(expected.Color, actual.Color);
                Assert.Equal(expected.R, actual32.R);
                Assert.Equal(expected.G, actual32.G);
                Assert.Equal(expected.B, actual32.B);
                Assert.Equal(expected.A, actual32.A);
            });
        }
    }
}
