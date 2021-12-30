using System.Drawing;
using System.IO;
using ImageMagitek.Builders;
using ImageMagitek.Codec;
using NUnit.Framework;

namespace ImageMagitek.UnitTests.BuilderTests;

[TestFixture]
public class ArrangerBuilderTests
{
    [Test]
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

        Assert.That(arranger.Layout == ElementLayout.Tiled);
        Assert.That(arranger.ArrangerElementSize == new Size(8, 16));
        Assert.That(arranger.ElementPixelSize == new Size(8, 8));
        Assert.That(arranger.ColorType == PixelColorType.Indexed);
        Assert.That(arranger.Name == "ScatteredTestArranger");
    }

    [Test]
    public void Build_SequentialArranger()
    {
        var codecFactory = new CodecFactory(new());
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

        Assert.That(arranger.Layout == ElementLayout.Single);
        Assert.That(arranger.ArrangerElementSize == new Size(1, 1));
        Assert.That(arranger.ElementPixelSize == new Size(256, 512));
        Assert.That(arranger.ColorType == PixelColorType.Direct);
        Assert.That(arranger.Name == "SequentialTestArranger");
        Assert.That(arranger.ActiveCodec.Name == "PSX 16bpp");
        Assert.That(arranger.ActiveCodec.ColorType == PixelColorType.Direct);
        Assert.That(arranger.ActiveCodec.Layout == ImageLayout.Single);
        Assert.That(arranger.ActiveCodec.Width == 256);
        Assert.That(arranger.ActiveCodec.Height == 512);
        Assert.That(arranger.ActivePalette is null);
        Assert.That(arranger.ActiveDataFile.Name == "TestMemoryFile");
    }

}
