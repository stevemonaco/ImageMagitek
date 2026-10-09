using System;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Image;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests;

public class ImageCopierTests
{
    private static readonly Palette _palette = ArrangerTestFactory.CreatePalette(
        Enumerable.Range(0, 16).Select(i => new ColorRgba32((byte)(i * 16), (byte)(255 - i * 16), (byte)(i * 8), 255)).ToArray());

    private static IndexedImage CreateImage(string codecName, byte fill)
    {
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 1, 1,
            (_, _) => CodecTestHelpers.CreateCodec(CodecFixture.Shared.CodecFactory, codecName, 8, 8, _palette));
        var image = new IndexedImage(arranger);
        Array.Fill(image.Image, fill);
        return image;
    }

    [Fact]
    public void CopyPixels_ExactIndex_IndexAboveDestDepth_Fails()
    {
        var source = CreateImage("SNES 4bpp", 15);
        var dest = CreateImage("SNES 2bpp", 0);

        var result = ImageCopier.CopyPixels(source, dest, Point.Empty, Point.Empty, 8, 8, PixelRemapOperation.RemapByExactIndex);

        Assert.True(result.HasFailed);
        Assert.All(dest.Image, x => Assert.Equal(0, x));
    }

    [Fact]
    public void CopyPixels_ExactIndexThenColors_IndexAboveDestDepth_UsesColors()
    {
        var source = CreateImage("SNES 4bpp", 2);
        source.Image[0] = 15;
        var dest = CreateImage("SNES 2bpp", 0);

        var palette = ArrangerTestFactory.CreatePalette(_palette.GetNativeColor(0), _palette.GetNativeColor(2), _palette.GetNativeColor(15), _palette.GetNativeColor(3));
        foreach (var element in dest.Arranger.EnumerateElements().OfType<ArrangerElement>())
            ((IIndexedCodec)element.Codec).Palette = palette;

        var result = ImageCopier.CopyPixels(source, dest, Point.Empty, Point.Empty, 8, 8,
            PixelRemapOperation.RemapByExactIndex, PixelRemapOperation.RemapByExactPaletteColors);

        Assert.False(result.HasFailed);
        Assert.Equal(2, dest.Image[0]);
        Assert.All(dest.Image.Skip(1), x => Assert.Equal(1, x));
    }

    [Fact]
    public void CopyPixels_ExactIndex_DestHoldsHighIndex_Succeeds()
    {
        var source = CreateImage("SNES 2bpp", 3);
        var dest = CreateImage("SNES 4bpp", 9);

        var result = ImageCopier.CopyPixels(source, dest, Point.Empty, Point.Empty, 8, 8, PixelRemapOperation.RemapByExactIndex);

        Assert.False(result.HasFailed);
        Assert.All(dest.Image, x => Assert.Equal(3, x));
    }
}
