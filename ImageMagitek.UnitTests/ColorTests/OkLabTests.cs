using ImageMagitek.Colors;
using Xunit;

namespace ImageMagitek.UnitTests;

public class OkLabTests
{
    private const double _tolerance = 1e-3;

    [Theory]
    [InlineData(255, 255, 255, 1.0, 0.0, 0.0)]
    [InlineData(0, 0, 0, 0.0, 0.0, 0.0)]
    [InlineData(255, 0, 0, 0.62796, 0.22486, 0.12585)]
    [InlineData(0, 255, 0, 0.86644, -0.23389, 0.17950)]
    [InlineData(0, 0, 255, 0.45201, -0.03246, -0.31153)]
    public void ToOkLab_MatchesReferenceValues(byte r, byte g, byte b, double expectedL, double expectedA, double expectedB)
    {
        var (l, a, bb) = OkLab.ToOkLab(new ColorRgba32(r, g, b, 255));

        Assert.Equal(expectedL, l, _tolerance);
        Assert.Equal(expectedA, a, _tolerance);
        Assert.Equal(expectedB, bb, _tolerance);
    }

    [Fact]
    public void DeltaE_BlackToWhite_IsOne()
    {
        Assert.Equal(1.0, OkLab.DeltaE(new ColorRgba32(0, 0, 0, 255), new ColorRgba32(255, 255, 255, 255)), _tolerance);
    }
}
