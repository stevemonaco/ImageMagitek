using System.Drawing;
using ImageMagitek.Builders;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using Xunit;

namespace ImageMagitek.UnitTests.BuilderTests;
public class ArrangerBuilderTests
{
    [Fact]
    public void Build_ScatteredArranger()
    {
        ScatteredArranger arranger = ArrangerBuilder
            .WithTiledLayout()
            .WithArrangerElementSize(8, 16)
            .WithElementPixelSize(8, 8)
            .WithPixelColorType(PixelColorType.Indexed)
            .WithName("ScatteredTestArranger")
            .AsScatteredArranger()
            .Build();

        Assert.Equal(ElementLayout.Tiled, arranger.Layout);
        Assert.Equal(new Size(8, 16), arranger.ArrangerElementSize);
        Assert.Equal(new Size(8, 8), arranger.ElementPixelSize);
        Assert.Equal(PixelColorType.Indexed, arranger.ColorType);
        Assert.Equal("ScatteredTestArranger", arranger.Name);
    }

    [Fact]
    public void Build_SequentialArranger()
    {
        var emptyPal = new Palette("Default", new ColorFactory(), ColorModel.Rgba32, true, PaletteStorageSource.GlobalJson);
        var codecFactory = new CodecFactory(emptyPal, new());
        var memorySource = new MemoryDataSource("TestMemoryFile", 2 * 256 * 512);

        SequentialArranger arranger = ArrangerBuilder
            .WithSingleLayout()
            .WithElementPixelSize(256, 512)
            .WithPixelColorType(PixelColorType.Direct)
            .WithName("SequentialTestArranger")
            .AsSequentialArranger(codecFactory)
            .WithDataFile(memorySource)
            .WithCodecName("PSX 16bpp")
            .Build();

        Assert.Equal(ElementLayout.Single, arranger.Layout);
        Assert.Equal(new Size(1, 1), arranger.ArrangerElementSize);
        Assert.Equal(new Size(256, 512), arranger.ElementPixelSize);
        Assert.Equal(PixelColorType.Direct, arranger.ColorType);
        Assert.Equal("SequentialTestArranger", arranger.Name);
        Assert.Equal("PSX 16bpp", arranger.ActiveCodec.Name);
        Assert.Equal(PixelColorType.Direct, arranger.ActiveCodec.ColorType);
        Assert.Equal(ImageLayout.Single, arranger.ActiveCodec.Layout);
        Assert.Equal(256, arranger.ActiveCodec.Width);
        Assert.Equal(512, arranger.ActiveCodec.Height);
        Assert.True(arranger.ActivePalette is null);
        Assert.Equal("TestMemoryFile", arranger.ActiveDataSource.Name);
    }

}
