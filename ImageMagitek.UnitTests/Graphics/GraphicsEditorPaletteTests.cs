using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using ImageMagitek.Services.Stores;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using TileShop.UI.Features.Graphics;
using Xunit;

namespace ImageMagitek.UnitTests.Graphics;

[Collection("Codec")]
public class GraphicsEditorPaletteTests
{
    private readonly CodecFixture _fixture;

    public GraphicsEditorPaletteTests(CodecFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Sequential_List_GlobalsInSettingsOrder()
    {
        var zeta = CreatePalette("Zeta", 0);
        var alpha = CreatePalette("Alpha", 40);
        var mid = CreatePalette("Mid", 80);
        var store = new PaletteStore(zeta, [zeta, alpha, mid]);
        var list = new EditorPaletteList(store);

        var palettes = list.Build(CreateArranger(zeta));

        Assert.Equal([zeta, alpha, mid], palettes);
    }

    [Fact]
    public void Sequential_Opens_OnDefaultPalette()
    {
        var store = CreateStore();
        var list = new EditorPaletteList(store);
        var arranger = CreateArranger(store.DefaultPalette);

        var selected = list.Select(arranger, list.Build(arranger));

        Assert.Same(store.DefaultPalette, selected);
    }

    [Fact]
    public void Sequential_ChangeCodec_KeepsPalette()
    {
        var store = CreateStore();
        var list = new EditorPaletteList(store);
        var arranger = CreateArranger(store.DefaultPalette);
        var chosen = store.GlobalPalettes[2];

        arranger.ChangePalette(chosen);
        arranger.ChangeCodec(_fixture.CodecFactory.CreateCodec("SNES 2bpp")!);

        Assert.Same(chosen, list.Select(arranger, list.Build(arranger)));
    }

    [Fact]
    public void Sequential_ChoosePalette_RecolorsKeepingIndices()
    {
        var store = CreateStore();
        var arranger = CreateArranger(store.DefaultPalette);
        var image = new ArrangerImageAdapter(arranger);
        image.SetIndexedPixel(0, 0, 3);
        var chosen = store.GlobalPalettes[1];

        arranger.ChangePalette(chosen);

        Assert.Equal(3, image.GetIndexedPixel(0, 0));
        Assert.Equal(chosen[3], image.IndexedImage!.GetPixelColor(0, 0));
    }

    [Fact]
    public void Sequential_ChoosePalette_LeavesCopiedElementsUnchanged()
    {
        var store = CreateStore();
        var arranger = CreateArranger(store.DefaultPalette);
        var copied = (ArrangerElement)arranger.GetElement(0, 0)!;

        arranger.ChangePalette(store.GlobalPalettes[1]);

        Assert.Same(store.DefaultPalette, ((IIndexedCodec)copied.Codec).Palette);
    }

    [Fact]
    public void Sequential_List_ExcludesPalettesTooSmallForCodec()
    {
        var store = CreateStore();
        var list = new EditorPaletteList(store);
        var arranger = CreateArranger(store.DefaultPalette);

        arranger.ChangeCodec(_fixture.CodecFactory.CreateCodec("SNES 8bpp")!);

        Assert.Empty(list.Build(arranger));
        Assert.Throws<System.ArgumentException>(() => arranger.ChangePalette(store.GlobalPalettes[1]));
    }

    [Fact]
    public void Sequential_ChangeCodec_FallsBackWhenPaletteTooSmall()
    {
        var store = CreateStore();
        var arranger = CreateArranger(store.DefaultPalette);
        var codec = _fixture.CodecFactory.CreateCodec("SNES 8bpp")!;
        var codecPalette = ((IIndexedCodec)codec).Palette;

        arranger.ChangeCodec(codec);

        Assert.Same(codecPalette, arranger.ActivePalette);
        Assert.Same(codecPalette, ((IIndexedCodec)arranger.GetElement(0, 0)!.Value.Codec).Palette);
    }

    private SequentialArranger CreateArranger(Palette palette)
    {
        var codec = _fixture.CodecFactory.CreateCodec("SNES 4bpp")!;
        return new SequentialArranger(4, 3, new MemoryDataSource("test", 4096), palette, _fixture.CodecFactory, codec);
    }

    private static PaletteStore CreateStore()
    {
        var palettes = new[] { CreatePalette("First", 0), CreatePalette("Second", 60), CreatePalette("Third", 120) };
        return new PaletteStore(palettes[0], palettes);
    }

    private static Palette CreatePalette(string name, int seed)
    {
        var palette = ArrangerTestFactory.CreatePalette(Enumerable.Range(0, 16)
            .Select(i => new ColorRgba32((byte)(seed + i * 8), (byte)(seed + i * 4), (byte)(i * 2), 255))
            .ToArray());
        palette.Name = name;
        return palette;
    }
}
