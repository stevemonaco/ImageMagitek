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

    [Fact]
    public void Bgr9_RawGenesisWord_UnpacksThreeBitChannels()
    {
        var color = new ColorBgr9(0x0EEE);

        Assert.Multiple(() =>
        {
            Assert.Equal(7, color.R);
            Assert.Equal(7, color.G);
            Assert.Equal(7, color.B);
            Assert.Equal(0x0EEEu, color.Color);
        });
    }
}
