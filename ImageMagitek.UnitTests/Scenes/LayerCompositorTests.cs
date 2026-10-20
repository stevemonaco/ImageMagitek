using System;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Scenes;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.Scenes;

public sealed class LayerCompositorTests
{
    private static readonly ColorRgba32 Clear = default;
    private static readonly ColorRgba32 Red = new(255, 0, 0, 255);
    private static readonly ColorRgba32 Green = new(0, 255, 0, 255);
    private static readonly ColorRgba32 Blue = new(0, 0, 255, 255);
    private static readonly ColorRgba32 Black = new(0, 0, 0, 255);

    private readonly ICodecFactory _codecFactory = CodecFixture.Shared.CodecFactory;

    private IIndexedCodec IndexedCodec(string name, Palette palette)
    {
        var codec = (IIndexedCodec)_codecFactory.CreateCodec(name)!;
        codec.Palette = palette;
        return codec;
    }

    private static Palette CreatePalette(bool zeroTransparent, params ColorRgba32[] colors)
    {
        var palette = ArrangerTestFactory.CreatePalette(colors);
        palette.ZeroIndexTransparent = zeroTransparent;
        return palette;
    }

    private static LayerImage Image(int width, int height, params ColorRgba32[] pixels) => new(width, height, pixels);

    private static LayerImage Solid(int width, int height, ColorRgba32 color) =>
        new(width, height, Enumerable.Repeat(color, width * height).ToArray());

    private static CompositorLayer Layer(LayerImage image, int x = 0, int y = 0, bool isVisible = true) =>
        new(image, new Point(x, y), isVisible);

    private static void SetSnes4bppIndex1(DataSource source, int element, int x) =>
        source.Write(new BitAddress(element * 32, 0), [(byte)(0x80 >> x)]);

    [Fact]
    public void LayerImage_IndexedArranger_UsesElementPalettes_ZeroTransparent()
    {
        var opaque = CreatePalette(false, Red, Green);
        var transparent = CreatePalette(true, Blue, Black);
        using var source = new MemoryDataSource("tiles", 64);
        SetSnes4bppIndex1(source, 0, 1);
        SetSnes4bppIndex1(source, 1, 1);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1,
            (x, _) => IndexedCodec("SNES 4bpp", x == 0 ? opaque : transparent), source);

        var image = LayerImage.FromArranger(arranger);

        Assert.Equal((16, 8), (image.Width, image.Height));
        Assert.Equal(Red, image.Pixels[0]);
        Assert.Equal(Green, image.Pixels[1]);
        Assert.Equal(Blue with { A = 0 }, image.Pixels[8]);
        Assert.Equal(Black, image.Pixels[9]);
        Assert.Equal(0, image.Pixels[8 + 16 * 7].A);
    }

    [Fact]
    public void LayerImage_DirectArranger_KeepsAlpha()
    {
        using var source = new MemoryDataSource("direct", 8 * 8 * 4);
        source.Write(new BitAddress(0), [10, 20, 30, 0x80, 40, 50, 60, 0xFF]);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Direct, 1, 1, (_, _) => _codecFactory.CreateCodec("Rgba32 Tiled")!, source);

        var image = LayerImage.FromArranger(arranger);

        Assert.Equal(new ColorRgba32(10, 20, 30, 0x80), image.Pixels[0]);
        Assert.Equal(new ColorRgba32(40, 50, 60, 0xFF), image.Pixels[1]);
        Assert.Equal(Clear, image.Pixels[2]);
    }

    [Fact]
    public void LayerImage_EmptyCell_IsTransparent()
    {
        var palette = CreatePalette(false, Red, Green);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => IndexedCodec("SNES 4bpp", palette));
        arranger.ResetElement(1, 0);
        var empty = new ScatteredArranger("empty", PixelColorType.Indexed, ElementLayout.Tiled, 2, 2, 8, 8);

        var image = LayerImage.FromArranger(arranger);
        var emptyImage = LayerImage.FromArranger(empty);

        Assert.Equal(Red, image.Pixels[0]);
        Assert.All(Enumerable.Range(0, 8).SelectMany(y => image.Pixels.Skip(y * 16 + 8).Take(8)), x => Assert.Equal(Clear, x));
        Assert.Equal((16, 16), (emptyImage.Width, emptyImage.Height));
        Assert.All(emptyImage.Pixels, x => Assert.Equal(Clear, x));
    }

    [Fact]
    public void LayerImage_OutsideSourceOrPastPalette_IsTransparent()
    {
        var shortPalette = CreatePalette(false, Red);
        using var source = new MemoryDataSource("tiles", 32);
        SetSnes4bppIndex1(source, 0, 1);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => IndexedCodec("SNES 4bpp", shortPalette), source);

        var image = LayerImage.FromArranger(arranger);

        Assert.Equal(Red, image.Pixels[0]);
        Assert.Equal(Clear, image.Pixels[1]);
        Assert.Equal(Clear, image.Pixels[8]);
    }

    [Fact]
    public void LayerImage_RereadsAfterWrite()
    {
        var palette = CreatePalette(false, Red, Green);
        using var source = new MemoryDataSource("tiles", 32);
        var arranger = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 1, 1, (_, _) => IndexedCodec("SNES 4bpp", palette), source);

        var before = LayerImage.FromArranger(arranger);
        SetSnes4bppIndex1(source, 0, 0);
        var after = LayerImage.FromArranger(arranger);

        Assert.Equal(Red, before.Pixels[0]);
        Assert.Equal(Green, after.Pixels[0]);
    }

    [Fact]
    public void Composite_DrawsLayersBackToFront()
    {
        var back = Solid(2, 1, Red);
        var front = Image(1, 1, Green);

        var result = LayerCompositor.Composite(new Size(2, 1), [Layer(back), Layer(front, 1, 0)]);
        var reversed = LayerCompositor.Composite(new Size(2, 1), [Layer(front, 1, 0), Layer(back)]);

        Assert.Equal([Red, Green], result.Pixels);
        Assert.Equal([Red, Red], reversed.Pixels);
        Assert.Equal([Red, Red], back.Pixels);
        Assert.Equal([Green], front.Pixels);
    }

    [Fact]
    public void Composite_TransparentPixelShowsLayerBelow()
    {
        var back = Solid(2, 1, Red);
        var front = Image(2, 1, Green with { A = 0 }, Blue);

        var result = LayerCompositor.Composite(new Size(2, 1), [Layer(back), Layer(front)]);

        Assert.Equal([Red, Blue], result.Pixels);
    }

    [Fact]
    public void Composite_PartialAlpha_BlendsSourceOver()
    {
        var back = Image(2, 1, new ColorRgba32(200, 100, 0, 255), new ColorRgba32(0, 0, 200, 128));
        var front = Solid(2, 1, new ColorRgba32(0, 50, 100, 64));

        var result = LayerCompositor.Composite(new Size(2, 1), [Layer(back), Layer(front)]);

        Assert.Equal(new ColorRgba32(150, 87, 25, 255), result.Pixels[0]);
        Assert.Equal(new ColorRgba32(0, 20, 160, 160), result.Pixels[1]);
    }

    [Fact]
    public void Composite_HiddenLayerSkipped()
    {
        var result = LayerCompositor.Composite(new Size(1, 1), [Layer(Image(1, 1, Red)), Layer(Image(1, 1, Green), isVisible: false)]);

        Assert.Equal([Red], result.Pixels);
    }

    [Fact]
    public void Composite_LayerPartlyOffCanvas_IsClipped()
    {
        var layer = Image(2, 2, Red, Green, Blue, Black);

        var negative = LayerCompositor.Composite(new Size(2, 2), [Layer(layer, -1, -1)]);
        var overhanging = LayerCompositor.Composite(new Size(2, 2), [Layer(layer, 1, 1)]);
        var outside = LayerCompositor.Composite(new Size(2, 2), [Layer(layer, 2, 0), Layer(layer, -2, 0), Layer(layer, 0, 5)]);

        Assert.Equal([Black, Clear, Clear, Clear], negative.Pixels);
        Assert.Equal([Clear, Clear, Clear, Red], overhanging.Pixels);
        Assert.All(outside.Pixels, x => Assert.Equal(Clear, x));
        Assert.Equal((2, 2), (outside.Width, outside.Height));
    }

    [Fact]
    public void Composite_BackdropShowsOnlyThroughTransparentPixels()
    {
        var back = Image(3, 1, Clear, Red, Clear);
        var front = Image(3, 1, Clear, Clear, Green);

        var result = LayerCompositor.Composite(new Size(3, 1), [Layer(back), Layer(front)], Blue);

        Assert.Equal([Blue, Red, Green], result.Pixels);
    }

    [Fact]
    public void Composite_NoBackdrop_IsTransparent()
    {
        var result = LayerCompositor.Composite(new Size(2, 1), [Layer(Image(1, 1, Red))]);

        Assert.Equal([Red, Clear], result.Pixels);
    }

    [Fact]
    public void Composite_8bppUnder4bpp()
    {
        var bottomPalette = CreatePalette(false, Red);
        var topPalette = CreatePalette(true, Blue, Green);
        using var tiles4 = new MemoryDataSource("4bpp", 32);
        SetSnes4bppIndex1(tiles4, 0, 0);
        var bottom = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 2, 1, (_, _) => IndexedCodec("GBA 8bpp", bottomPalette));
        var top = ArrangerTestFactory.CreateArranger(PixelColorType.Indexed, 1, 1, (_, _) => IndexedCodec("SNES 4bpp", topPalette), tiles4);

        var result = LayerCompositor.Composite(new Size(16, 8),
            [Layer(LayerImage.FromArranger(bottom)), Layer(LayerImage.FromArranger(top), 8, 0)]);

        Assert.Equal(Red, result.Pixels[0]);
        Assert.Equal(Green, result.Pixels[8]);
        Assert.Equal(Red, result.Pixels[9]);
        Assert.Equal(Red, result.Pixels[16 * 8 - 1]);
    }

    [Fact]
    public void Composite_NoLayers_IsBackdrop()
    {
        var result = LayerCompositor.Composite(new Size(2, 2), [], Blue);

        Assert.All(result.Pixels, x => Assert.Equal(Blue, x));
        Assert.Equal((2, 2), (result.Width, result.Height));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(-1, 4)]
    public void Composite_ZeroCanvas_Throws(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LayerCompositor.Composite(new Size(width, height), [Layer(Image(1, 1, Red))]));
}
