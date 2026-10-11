using ImageMagitek.Colors;
using TileShop.Shared.Models;
using TileShop.UI.Features.Graphics;
using Xunit;

namespace ImageMagitek.UnitTests.Graphics;

public class ColorDescriptionTests
{
    private static readonly ColorRgba32 _white = new(255, 255, 255, 255);

    [Fact]
    public void DescribeIndexed_Hex_ShowsPaddedForeignAndNative()
    {
        var text = ColorDescription.DescribeIndexed(1, 2, 3, ColorModel.Bgr15, new ColorBgr15(0x1F), _white, ForeignColorFormat.Hex);

        Assert.Equal("Color (1, 2): Index 3, Bgr15 0x001F, rgba(255, 255, 255, 255)", text);
    }

    [Fact]
    public void DescribeIndexed_Channels_ShowsForeignChannels()
    {
        var text = ColorDescription.DescribeIndexed(1, 2, 3, ColorModel.Bgr15, new ColorBgr15(31, 0, 7), _white, ForeignColorFormat.Channels);

        Assert.Equal("Color (1, 2): Index 3, Bgr15 (31, 0, 7), rgba(255, 255, 255, 255)", text);
    }

    [Fact]
    public void DescribeIndexed_ChannelsWithAlpha_IncludesAlpha()
    {
        var text = ColorDescription.DescribeIndexed(0, 0, 0, ColorModel.Abgr16, new ColorAbgr16(1, 2, 3, 1), _white, ForeignColorFormat.Channels);

        Assert.Equal("Color (0, 0): Index 0, Abgr16 (1, 2, 3, 1), rgba(255, 255, 255, 255)", text);
    }

    [Fact]
    public void DescribeIndexed_TableColorChannels_ShowsDecimalEntry()
    {
        var text = ColorDescription.DescribeIndexed(0, 0, 0, ColorModel.Nes, new ColorNes(0x30), _white, ForeignColorFormat.Channels);

        Assert.Equal("Color (0, 0): Index 0, Nes 48, rgba(255, 255, 255, 255)", text);
    }

    [Theory]
    [InlineData(ForeignColorFormat.Hex)]
    [InlineData(ForeignColorFormat.Channels)]
    public void DescribeIndexed_Rgba32Palette_OmitsForeign(ForeignColorFormat format)
    {
        var text = ColorDescription.DescribeIndexed(4, 5, 6, ColorModel.Rgba32, _white, _white, format);

        Assert.Equal("Color (4, 5): Index 6, rgba(255, 255, 255, 255)", text);
    }

    [Fact]
    public void DescribeDirect_ShowsNative()
    {
        Assert.Equal("Color (7, 8): rgba(1, 2, 3, 4)", ColorDescription.DescribeDirect(7, 8, new ColorRgba32(1, 2, 3, 4)));
    }
}
