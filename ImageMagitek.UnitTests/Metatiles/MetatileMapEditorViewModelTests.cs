using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.Tilemaps;
using SkiaSharp;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Metatiles;

public sealed class MetatileMapEditorViewModelTests
{
    [Fact]
    public void HidingLayer_RedrawsWithoutIt()
    {
        var formats = MapFormatTests.LoadShippedFormats();
        var palette = new Palette("zero transparent", new ColorFactory(), ColorModel.Rgba32, true, PaletteStorageSource.GlobalJson);
        palette.SetColorSources(new[] { new ColorRgba32(0, 0, 0, 255), new ColorRgba32(255, 0, 0, 255), new ColorRgba32(0, 255, 0, 255) }
            .Select(x => new ProjectNativeColorSource(x)));

        using var tiles = new MemoryDataSource("tiles");
        tiles.Write([.. Enumerable.Repeat((byte)0x11, 32), .. Enumerable.Repeat((byte)0x22, 32)]);
        using var table = new MemoryDataSource("table");
        table.Write([0x00, 0x00, 0x01, 0x00]);
        using var blocks = new MemoryDataSource("blocks");
        blocks.Write([0x00, 0x00]);

        var set = new MetatileSet("Set", table, 0, 1, new Size(1, 1), 2, formats.Find("GBA Text BG")!, MetatileOrder.Interleaved,
            tiles, 0, "GBA 4bpp", 0, null, [palette], CodecFixture.Shared.CodecFactory);
        using var map = new MetatileMap("Map", blocks, 0, formats.Find("FRLG Block")!, MapScreenLayout.Linear, 1, 1,
            [new MetatileMapSet(set, 0)]);
        var editor = new MetatileMapEditorViewModel(map, new GridPreferences(), () => Task.CompletedTask);
        Assert.Equal(["Bottom", "Top"], editor.Layers.Select(x => x.Name));
        Assert.Equal(SKColors.Lime, editor.Bitmap.Bitmap.GetPixel(0, 0));

        editor.Layers[1].IsVisible = false;

        Assert.False(map.IsLayerVisible(1));
        Assert.Equal(SKColors.Red, editor.Bitmap.Bitmap.GetPixel(0, 0));
    }
}
