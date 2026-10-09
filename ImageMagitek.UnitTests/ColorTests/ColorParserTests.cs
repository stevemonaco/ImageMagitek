using ImageMagitek.Colors;
using ImageMagitek.Utility.Parsing;
using Xunit;

namespace ImageMagitek.UnitTests.ColorTests;

public class ColorParserTests
{
    [Theory]
    [InlineData("#00", true)]
    [InlineData("#3F", true)]
    [InlineData("#40", false)]
    [InlineData("#FF", false)]
    public void TryParse_Nes(string input, bool expected)
    {
        var isParsed = ColorParser.TryParse(input, ColorModel.Nes, out var color);

        Assert.Equal(expected, isParsed);
        if (expected)
            Assert.Equal(System.Convert.ToUInt32(input[1..], 16), Assert.IsType<ColorNes>(color).Color);
    }
}
