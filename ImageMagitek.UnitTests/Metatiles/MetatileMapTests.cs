using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using ImageMagitek.UnitTests.Tilemaps;
using Xunit;

namespace ImageMagitek.UnitTests.Metatiles;

public sealed class MetatileMapTests : IDisposable
{
    private const int TileBytes = 32;

    private static readonly MapFormatFactory _formats = MapFormatTests.LoadShippedFormats();
    private static readonly MapFormat _gba = _formats.Find("GBA Text BG")!;
    private static readonly MapFormat _frlg = _formats.Find("FRLG Block")!;

    private readonly ICodecFactory _codecFactory = CodecFixture.Shared.CodecFactory;
    private readonly Palette _slot0 = CreatePalette(10);
    private readonly Palette _slot1 = CreatePalette(20);
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();
    }

    private T Track<T>(T disposable) where T : IDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }

    private static Palette CreatePalette(byte seed) =>
        ArrangerTestFactory.CreatePalette(Enumerable.Range(0, 16).Select(i => new ColorRgba32((byte)(seed + i), 0, 0, 255)).ToArray());

    private MemoryDataSource Source(string name, byte[] bytes)
    {
        var source = Track(new MemoryDataSource(name));
        source.Write(bytes);
        return source;
    }

    /// <summary>
    /// A tile source whose tile n has every byte equal to n, so tiles decode differently
    /// </summary>
    private MemoryDataSource Tiles(string name, int count) =>
        Source(name, Enumerable.Range(0, count).SelectMany(i => Enumerable.Repeat((byte)i, TileBytes)).ToArray());

    private static byte[] Entry(int tile, int palette = 0, bool hflip = false, bool vflip = false)
    {
        var value = tile | palette << 12 | (hflip ? 1 << 10 : 0) | (vflip ? 1 << 11 : 0);
        return [(byte)value, (byte)(value >> 8)];
    }

    private static byte[] Blocks(params int[] values) => values.SelectMany(x => new[] { (byte)x, (byte)(x >> 8) }).ToArray();

    private MetatileSet Set(string name, DataSource table, int count, DataSource tiles, int firstTile, Palette?[] slots,
        int layers = 2, int width = 2, int height = 2, MetatileOrder order = MetatileOrder.Interleaved) =>
        new(name, table, 0, count, new Size(width, height), layers, _gba, order, tiles, 0, "GBA 4bpp", firstTile, null, slots, _codecFactory);

    private MetatileMap Map(DataSource blocks, int width, int height, params MetatileMapSet[] sets) =>
        Track(new MetatileMap("Map", blocks, 0, _frlg, MapScreenLayout.Linear, width, height, sets));

    private static MirrorOperation Mirror(bool hflip, bool vflip) => new MapEntry(0, 0, hflip, vflip, false).Mirror;

    [Theory]
    [InlineData(MetatileOrder.Interleaved)]
    [InlineData(MetatileOrder.Planar)]
    public void LayerArrangers_MatchHandBuiltScatteredArrangers(MetatileOrder order)
    {
        const int count = 3, layers = 2;
        var tiles = Tiles("tiles", 16);
        var table = new byte[count * layers * 4 * 2];

        var codecs = new[] { _slot0, _slot1 }.Select(palette =>
        {
            var codec = (IIndexedCodec)_codecFactory.CreateCodec("GBA 4bpp")!;
            codec.Palette = palette;
            return codec;
        }).ToArray();

        (int Tile, int Palette, bool HFlip, bool VFlip) Describe(int m, int l, int x, int y) =>
            ((m * 8 + l * 4 + y * 2 + x) % 16, (m + l + x) % 2, (x + m) % 2 == 1, (y + l) % 2 == 1);

        for (int m = 0; m < count; m++)
            for (int l = 0; l < layers; l++)
                for (int y = 0; y < 2; y++)
                    for (int x = 0; x < 2; x++)
                    {
                        var (tile, palette, hflip, vflip) = Describe(m, l, x, y);
                        var index = order.EntryIndex(m, l, x, y, 2, 2, layers, count);
                        Entry(tile, palette, hflip, vflip).CopyTo(table, index * 2);
                    }

        int[] blockNumbers = [0, 1, 2, 2, 1, 0];
        var set = Set("Set", Source("table", table), count, tiles, 0, [_slot0, _slot1], order: order);
        var map = Map(Source("blocks", Blocks(blockNumbers.Select((n, i) => n | (i << 10)).ToArray())), 3, 2, new MetatileMapSet(set, 0));

        for (int l = 0; l < layers; l++)
        {
            var expected = new ScatteredArranger("expected", PixelColorType.Indexed, ElementLayout.Tiled, 6, 4, 8, 8);
            for (int by = 0; by < 2; by++)
                for (int bx = 0; bx < 3; bx++)
                    for (int y = 0; y < 2; y++)
                        for (int x = 0; x < 2; x++)
                        {
                            var (tile, palette, hflip, vflip) = Describe(blockNumbers[by * 3 + bx], l, x, y);
                            expected.SetElement(new ArrangerElement(0, 0, tiles, new BitAddress(tile * TileBytes, 0), codecs[palette],
                                Mirror(hflip, vflip), RotationOperation.None), bx * 2 + x, by * 2 + y);
                        }

            Assert.Null(map.MapError);
            TilemapArrangerTests.AssertSameElements(expected, map.Layers[l]);
            Assert.Equal(new IndexedImage(expected).Image, new IndexedImage(map.Layers[l]).Image);
        }
    }

    [Fact]
    public void LayerArranger_SetElement_Throws()
    {
        var set = Set("Set", Source("table", new byte[16]), 1, Tiles("tiles", 1), 0, [_slot0]);
        var layer = Map(Source("blocks", Blocks(0)), 1, 1, new MetatileMapSet(set, 0)).Layers[0];
        var element = layer.GetElement(0, 0);

        Assert.Throws<InvalidOperationException>(() => layer.SetElement(element, 0, 0));
        Assert.Throws<InvalidOperationException>(() => layer.ResetElement(0, 0));
        Assert.Throws<InvalidOperationException>(() => layer.Resize(2, 2));
    }

    private (MetatileSet A, MetatileSet B) DisjointSets(int countA = 3, int countB = 3)
    {
        var a = Set("A", Source("tableA", new byte[64]), countA, Tiles("tilesA", 4), 0, [_slot0]);
        var b = Set("B", Source("tableB", new byte[64]), countB, Tiles("tilesB", 4), 4, [null, _slot1]);
        return (a, b);
    }

    [Fact]
    public void Create_OverlappingMetatileRanges_Throws()
    {
        var (a, b) = DisjointSets();
        Map(Source("ok", Blocks(0)), 1, 1, new MetatileMapSet(a, 0), new MetatileMapSet(b, 3));

        var ex = Assert.Throws<ArgumentException>(() => Map(Source("blocks", Blocks(0)), 1, 1, new MetatileMapSet(a, 0), new MetatileMapSet(b, 2)));

        Assert.Contains("Metatile map 'Map'", ex.Message);
        Assert.Contains("metatile numbers overlap", ex.Message);
    }

    [Fact]
    public void Create_OverlappingTileRanges_Throws()
    {
        var (a, _) = DisjointSets();
        var b = Set("B", Source("tableB", new byte[64]), 3, Tiles("tilesB", 4), 2, [null, _slot1]);

        var ex = Assert.Throws<ArgumentException>(() => Map(Source("blocks", Blocks(0)), 1, 1, new MetatileMapSet(a, 0), new MetatileMapSet(b, 3)));

        Assert.Contains("tile ranges overlap", ex.Message);
    }

    [Fact]
    public void Create_SharedPaletteSlot_Throws()
    {
        var (a, _) = DisjointSets();
        var b = Set("B", Source("tableB", new byte[64]), 3, Tiles("tilesB", 4), 4, [_slot1]);

        var ex = Assert.Throws<ArgumentException>(() => Map(Source("blocks", Blocks(0)), 1, 1, new MetatileMapSet(a, 0), new MetatileMapSet(b, 3)));

        Assert.Contains("palette slot 0", ex.Message);
    }

    [Fact]
    public void Create_MismatchedShape_Throws()
    {
        var (a, _) = DisjointSets();
        var b = Set("B", Source("tableB", new byte[64]), 3, Tiles("tilesB", 4), 4, [null, _slot1], width: 1, height: 1);
        var c = Set("C", Source("tableC", new byte[64]), 3, Tiles("tilesC", 4), 4, [null, _slot1], layers: 1);

        var shapeEx = Assert.Throws<ArgumentException>(() => Map(Source("blocks", Blocks(0)), 1, 1, new MetatileMapSet(a, 0), new MetatileMapSet(b, 3)));
        var layersEx = Assert.Throws<ArgumentException>(() => Map(Source("blocks", Blocks(0)), 1, 1, new MetatileMapSet(a, 0), new MetatileMapSet(c, 3)));

        Assert.Contains("shapes must match", shapeEx.Message);
        Assert.Contains("layers", layersEx.Message);
    }

    [Fact]
    public void FrlgBlock_DecodesMetatileNumber()
    {
        Assert.Equal(new MapField(0, 10), _frlg.TileIndex);
        Assert.Equal(16, _frlg.EntryBits);

        var set = Set("Set", Source("table", new byte[3 * 16]), 3, Tiles("tiles", 4), 0, [_slot0]);
        var map = Map(Source("blocks", [0x82, 0xFE]), 1, 1, new MetatileMapSet(set, 640));

        Assert.Equal(0x282, _frlg.TileIndex.Extract(0xFE82u));
        Assert.NotNull(map.Layers[0].GetElement(0, 0));
    }

    [Fact]
    public void BlockBitsValue_RemovesIndexField()
    {
        var set = Set("Set", Source("table", new byte[16]), 1, Tiles("tiles", 1), 0, [_slot0]);
        var map = Map(Source("blocks", [0x82, 0xFE]), 1, 1, new MetatileMapSet(set, 0));

        Assert.Equal(0x3F, map.BlockBitsValue(0, 0));
        Assert.Equal(0b1011u, new MapField(2, 3).Remove(0b10_111_11u));
    }

    /// <summary>
    /// A primary set at base 0 with 640 metatiles over tiles 0-639, whose metatile m uses tiles 8m to 8m+7 (mod 640) with palette field 1,
    /// and a secondary set at base 640 with tiles from 640. Secondary metatile 0 uses tile 1000 at (0, 0); metatile 1 uses tiles 5, 641, 0 and 640
    /// on its bottom layer.
    /// </summary>
    private (MetatileMap Map, DataSource PrimaryTiles, DataSource SecondaryTiles) TwoSetMap(params int[] blocks)
    {
        var primaryTiles = Tiles("primary tiles", 640);
        var primaryTable = Enumerable.Range(0, 640 * 8).SelectMany(e => Entry(e % 640, palette: 1)).ToArray();
        var primary = Set("Primary", Source("primary table", primaryTable), 640, primaryTiles, 0, [_slot0]);

        var secondaryTiles = Tiles("secondary tiles", 16);
        byte[] secondaryTable =
        [
            .. Entry(1000), .. Entry(641), .. Entry(642), .. Entry(643), .. new byte[8],
            .. Entry(5), .. Entry(641, palette: 1), .. Entry(0, palette: 7), .. Entry(640), .. new byte[8]
        ];
        var secondary = Set("Secondary", Source("secondary table", secondaryTable), 2, secondaryTiles, 640, [null, _slot1]);

        var map = Map(Source("blocks", Blocks(blocks)), blocks.Length, 1, new MetatileMapSet(primary, 0), new MetatileMapSet(secondary, 640));
        return (map, primaryTiles, secondaryTiles);
    }

    [Fact]
    public void Block_ResolvesAcrossSets()
    {
        var (map, primaryTiles, secondaryTiles) = TwoSetMap(641, 5);
        var layer = map.Layers[0];

        Assert.Same(secondaryTiles, layer.GetElement(1, 1)!.Value.Source);
        Assert.Equal(0, layer.GetElement(1, 1)!.Value.SourceAddress.ByteOffset);
        Assert.Equal(40 * TileBytes, layer.GetElement(2, 0)!.Value.SourceAddress.ByteOffset);
        Assert.Equal(44 * TileBytes, map.Layers[1].GetElement(2, 0)!.Value.SourceAddress.ByteOffset);
        Assert.Same(primaryTiles, layer.GetElement(2, 0)!.Value.Source);
    }

    [Fact]
    public void Tile_ResolvesAcrossSets()
    {
        var (map, primaryTiles, secondaryTiles) = TwoSetMap(641);
        var layer = map.Layers[0];

        Assert.Same(primaryTiles, layer.GetElement(0, 0)!.Value.Source);
        Assert.Equal(5 * TileBytes, layer.GetElement(0, 0)!.Value.SourceAddress.ByteOffset);
        Assert.Same(secondaryTiles, layer.GetElement(1, 0)!.Value.Source);
        Assert.Equal(1 * TileBytes, layer.GetElement(1, 0)!.Value.SourceAddress.ByteOffset);
        Assert.Same(primaryTiles, layer.GetElement(0, 1)!.Value.Source);
    }

    [Fact]
    public void Block_OutsideEverySet_IsEmpty()
    {
        var (map, _, _) = TwoSetMap(700, 0);

        Assert.All(map.Layers, layer => Assert.All(layer.EnumerateElements(0, 0, 2, 2), Assert.Null));
        Assert.NotNull(map.Layers[0].GetElement(2, 0));
        Assert.Null(map.MapError);
    }

    [Fact]
    public void Tile_OutsideEveryTileSet_IsEmpty()
    {
        var (map, _, _) = TwoSetMap(640);

        Assert.Null(map.Layers[0].GetElement(0, 0));
        Assert.NotNull(map.Layers[0].GetElement(1, 0));
    }

    [Fact]
    public void PaletteSlots_CombineAcrossSets()
    {
        var (map, _, _) = TwoSetMap(641, 0);
        Palette PaletteAt(int x, int y) => ((IIndexedCodec)map.Layers[0].GetElement(x, y)!.Value.Codec).Palette;

        Assert.Same(_slot0, map.PaletteSlots[0]);
        Assert.Same(_slot1, map.PaletteSlots[1]);
        Assert.Same(_slot0, PaletteAt(0, 0));
        Assert.Same(_slot1, PaletteAt(1, 0));
        Assert.Same(_slot0, PaletteAt(0, 1));
        Assert.Same(_slot1, PaletteAt(2, 0));
    }

    [Fact]
    public void TruncatedBlockMap_SetsMapError()
    {
        var set = Set("Set", Source("table", new byte[16]), 1, Tiles("tiles", 1), 0, [_slot0], layers: 1, width: 1, height: 1);
        var map = Map(Source("blocks", Blocks(0, 0, 0)), 2, 2, new MetatileMapSet(set, 0));

        Assert.Contains("holds 3 of the 4 blocks a 2x2 map needs", map.MapError);
        Assert.NotNull(map.Layers[0].GetElement(0, 1));
        Assert.Null(map.Layers[0].GetElement(1, 1));
        Assert.Null(map.BlockBitsValue(1, 1));
    }

    private static readonly ColorRgba32 _color0 = new(10, 20, 30, 255);
    private static readonly ColorRgba32 _red = new(255, 0, 0, 255);
    private static readonly ColorRgba32 _green = new(0, 255, 0, 255);

    /// <summary>
    /// A one-block map of a 2x1-tile, two-layer metatile: tile 1 (red) twice on the bottom layer, tiles 0 (color 0) and 2 (green) on top
    /// </summary>
    private MetatileMap CompositeMap(int bottomTile = 1)
    {
        var palette = new Palette("zero transparent", new ColorFactory(), ColorModel.Rgba32, true, PaletteStorageSource.GlobalJson);
        palette.SetColorSources(new[] { _color0, _red, _green }.Select(x => new ProjectNativeColorSource(x)));

        var tiles = Source("tiles", [.. new byte[32], .. Enumerable.Repeat((byte)0x11, 32), .. Enumerable.Repeat((byte)0x22, 32)]);
        var table = Source("table", [.. Entry(bottomTile), .. Entry(bottomTile), .. Entry(0), .. Entry(bottomTile == 0 ? 0 : 2)]);
        var set = Set("Set", table, 1, tiles, 0, [palette], layers: 2, width: 2, height: 1);
        return Map(Source("blocks", Blocks(0)), 1, 1, new MetatileMapSet(set, 0));
    }

    private static ColorRgba32 PixelAt(ImageMagitek.Scenes.LayerImage image, int x, int y) => image.Pixels[y * image.Width + x];

    [Fact]
    public void Composite_TopLayerColorZeroShowsBottom()
    {
        var composite = CompositeMap().Composite();

        Assert.Equal((16, 8), (composite.Width, composite.Height));
        Assert.Equal(_red, PixelAt(composite, 0, 0));
        Assert.Equal(_green, PixelAt(composite, 8, 0));
    }

    [Fact]
    public void Composite_HiddenLayerSkipped()
    {
        var map = CompositeMap();
        map.SetLayerVisible(1, false);

        var composite = map.Composite();

        Assert.False(map.IsLayerVisible(1));
        Assert.Equal(_red, PixelAt(composite, 8, 0));
    }

    [Fact]
    public void Composite_BackdropIsSlot0Color0()
    {
        var map = CompositeMap(bottomTile: 0);

        var composite = map.Composite();

        Assert.Equal(_color0, map.Backdrop);
        Assert.All(composite.Pixels, x => Assert.Equal(_color0, x));
    }

    [Fact]
    public void RebuildsAfterBlockMapWrite()
    {
        var tiles = Tiles("tiles", 8);
        var set = Set("Set", Source("table", [.. Entry(1), .. Entry(2)]), 2, tiles, 0, [_slot0], layers: 1, width: 1, height: 1);
        var blocks = Source("blocks", Blocks(0));
        var map = Map(blocks, 1, 1, new MetatileMapSet(set, 0));
        Assert.Equal(1 * TileBytes, map.Layers[0].GetElement(0, 0)!.Value.SourceAddress.ByteOffset);

        blocks.Write(new BitAddress(0, 0), Blocks(1));
        blocks.NotifyDataWritten();

        Assert.Equal(2 * TileBytes, map.Layers[0].GetElement(0, 0)!.Value.SourceAddress.ByteOffset);
    }

    [Fact]
    public void ExportComposite_WritesCompositePng()
    {
        var composite = CompositeMap().Composite();
        var fileName = TestPaths.CreateTempPath(".png");

        try
        {
            var adapter = new ImageSharpFileAdapter();
            composite.ExportImage(fileName, adapter);

            var loaded = adapter.LoadImage(fileName).AsSuccess.Result;
            Assert.Equal((composite.Width, composite.Height), (loaded.Width, loaded.Height));
            Assert.Equal(composite.Pixels, loaded.Pixels);
        }
        finally
        {
            File.Delete(fileName);
        }
    }
}
