using System;
using System.Drawing;
using ImageMagitek.Colors;
using ImageMagitek.PluginSample;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.ArrangerTests;

public class ScatteredArrangerTests
{
    private static readonly Palette _palette = TestImageGenerator.CreateDistinctPalette(4);

    private static ScatteredArranger Create(int elemsX, int elemsY, int elementWidth = 8, int elementHeight = 8) =>
        ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, elemsX, elemsY,
            (_, _) => new Psx4BppCodec(_palette, elementWidth, elementHeight));

    [Fact]
    public void ReplaceElements_Larger_TakesSizeAndCells()
    {
        var dest = Create(2, 1);
        var source = Create(3, 2);

        dest.ReplaceElements(source);

        Assert.Equal(new Size(3, 2), dest.ArrangerElementSize);
        AssertSameCells(source, dest);
    }

    [Fact]
    public void ReplaceElements_Smaller_DropsOuterCells()
    {
        var dest = Create(3, 2);
        var source = Create(1, 1);

        dest.ReplaceElements(source);

        Assert.Equal(new Size(1, 1), dest.ArrangerElementSize);
        AssertSameCells(source, dest);
    }

    [Fact]
    public void ReplaceElements_EmptyCells_AreReset()
    {
        var dest = Create(2, 2);
        var source = Create(2, 2);
        source.ResetElement(1, 0);
        source.ResetElement(0, 1);

        dest.ReplaceElements(source);

        Assert.Null(dest.GetElement(1, 0));
        Assert.Null(dest.GetElement(0, 1));
        AssertSameCells(source, dest);
    }

    [Fact]
    public void ReplaceElements_DifferentElementSize_Throws()
    {
        var dest = Create(2, 2);
        var source = Create(2, 2, 16, 8);

        Assert.Throws<ArgumentException>(() => dest.ReplaceElements(source));
        Assert.Equal(new Size(2, 2), dest.ArrangerElementSize);
    }

    private static void AssertSameCells(ScatteredArranger expected, ScatteredArranger actual)
    {
        for (int y = 0; y < expected.ArrangerElementSize.Height; y++)
        {
            for (int x = 0; x < expected.ArrangerElementSize.Width; x++)
            {
                var expectedElement = expected.GetElement(x, y);
                var actualElement = actual.GetElement(x, y);

                Assert.Equal(expectedElement is null, actualElement is null);
                if (expectedElement is { } e && actualElement is { } a)
                {
                    Assert.Same(e.Source, a.Source);
                    Assert.Equal(e.SourceAddress, a.SourceAddress);
                    Assert.Same(e.Codec, a.Codec);
                }
            }
        }
    }
}
