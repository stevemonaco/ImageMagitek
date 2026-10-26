using System;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.Tilemaps;

/// <summary>
/// Plots tiles into the working copy of an 8x4 GBA tilemap whose map and tiles share one data source, with sentinel bytes before and after the map
/// </summary>
public sealed class TilePlottingTests : IDisposable
{
    private const int MapOffset = 16;
    private const int Width = 8;
    private const int Height = 4;
    private const int MapBytes = Width * Height * 2;
    private const int TileOffset = MapOffset + MapBytes + 16;
    private const int TileCount = 16;
    private const int TileBytes = 32;

    private static readonly MapFormatFactory _formats = MapFormatTests.LoadShippedFormats();
    private static readonly MapFormat _gba = _formats.Find("GBA Text BG")!;
    private static readonly MapFormat _plain8 = _formats.Find("Plain 8-bit Index")!;
    private static readonly MapFormat _mode7 = _formats.Find("SNES Mode 7")!;

    private readonly ICodecFactory _codecFactory = CodecFixture.Shared.CodecFactory;
    private readonly Palette _slot0 = CreatePalette(10);
    private readonly Palette _slot1 = CreatePalette(40);
    private readonly Palette _unslotted = CreatePalette(80);
    private readonly MemoryDataSource _rom = new("rom");

    public void Dispose() => _rom.Dispose();

    private static Palette CreatePalette(byte seed) =>
        ArrangerTestFactory.CreatePalette(Enumerable.Range(0, 16).Select(i => new ColorRgba32((byte)(seed + i), 0, 0, 255)).ToArray());

    private static byte[] Gba(int tile, int palette = 0, bool hflip = false, bool vflip = false)
    {
        var value = tile | palette << 12 | (hflip ? 1 << 10 : 0) | (vflip ? 1 << 11 : 0);
        return [(byte)value, (byte)(value >> 8)];
    }

    /// <summary>
    /// Writes a map whose cell (x, y) is tile (y * width + x) mod 16 unless <paramref name="cells"/> names it
    /// </summary>
    private void WriteRom(params (int X, int Y, byte[] Entry)[] cells)
    {
        var map = new byte[MapBytes];
        for (int i = 0; i < Width * Height; i++)
            Gba(i % TileCount).CopyTo(map, i * 2);

        foreach (var (x, y, entry) in cells)
            entry.CopyTo(map, (y * Width + x) * 2);

        var tiles = Enumerable.Range(0, TileCount * TileBytes).Select(x => (byte)(x * 7)).ToArray();
        _rom.Write([.. Enumerable.Repeat((byte)0xAA, MapOffset), .. map, .. Enumerable.Repeat((byte)0xBB, 16), .. tiles]);
    }

    private byte[] ReadRom() => _rom.Read(BitAddress.Zero, (int)_rom.Length * 8);

    private static byte[] WithEntry(byte[] rom, int x, int y, byte[] entry)
    {
        var expected = (byte[])rom.Clone();
        entry.CopyTo(expected, MapOffset + (y * Width + x) * 2);
        return expected;
    }

    private TilemapArranger Create(MapFormat format, int firstTile = 0, long tileOffset = TileOffset, int emptyTile = 0) =>
        new("tilemap", _rom, MapOffset, format, MapScreenLayout.Linear, Width, Height, _rom, tileOffset, "GBA 4bpp", firstTile,
            [_slot0, _slot1], _codecFactory, emptyTile);

    private ArrangerElement TileElement(int tile, Palette palette, DataSource? source = null, string codecName = "GBA 4bpp", int byteOffset = 0)
    {
        var codec = (IIndexedCodec)_codecFactory.CreateCodec(codecName)!;
        codec.Palette = palette;
        return new ArrangerElement(0, 0, source ?? _rom, new BitAddress(TileOffset + tile * TileBytes + byteOffset, 0), codec);
    }

    private static int TileAt(TilemapArranger tilemap, TilemapGrid working, int x, int y) => working[x, y]!.Value.TileIndex;

    private static TileBrush Brush(int width, int height, params int[] tiles)
    {
        var cells = new TileBrushCell?[height, width];
        for (int i = 0; i < tiles.Length; i++)
            cells[i / width, i % width] = new TileBrushCell(tiles[i], MirrorOperation.None);
        return new TileBrush(cells);
    }

    [Fact]
    public void TileCount_CappedByTileSetEnd()
    {
        WriteRom();
        using var tilemap = Create(_gba);

        Assert.Equal(TileCount, tilemap.TileCount);
    }

    [Fact]
    public void TileCount_CappedByTileField()
    {
        WriteRom();
        using var tilemap = Create(_plain8, firstTile: 250);

        Assert.Equal(6, tilemap.TileCount);
    }

    [Fact]
    public void TileCount_CappedByDeclaredCount_AndHigherTilesRenderEmpty()
    {
        WriteRom();
        using var tilemap = new TilemapArranger("tilemap", _rom, MapOffset, _gba, MapScreenLayout.Linear, Width, Height, _rom, TileOffset,
            "GBA 4bpp", 0, [_slot0, _slot1], _codecFactory, declaredTileCount: 4);

        Assert.Equal(4, tilemap.TileCount);
        Assert.Equal(4, TilePalette.Generate(tilemap, 0).Arranger.EnumerateElements().Count(x => x is not null));
        Assert.NotNull(tilemap.GetElement(3, 0));
        Assert.Null(tilemap.GetElement(4, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DeclaredTileCount_NotPositive_Throws(int count)
    {
        WriteRom();
        Assert.Throws<ArgumentException>(() => new TilemapArranger("tilemap", _rom, MapOffset, _gba, MapScreenLayout.Linear, Width, Height,
            _rom, TileOffset, "GBA 4bpp", 0, [_slot0, _slot1], _codecFactory, declaredTileCount: count));
    }

    [Fact]
    public void MostUsedPaletteSlot_CountsEmptySlotsAsSlotZero()
    {
        // 12 cells name slot 1, 9 name slot 0 and 11 name empty slot 5, which only wins for slot 0 when counted as slot 0
        WriteRom([.. Enumerable.Range(0, Width * Height).Select(i => (i % Width, i / Width, Gba(1, palette: i < 12 ? 1 : i < 21 ? 0 : 5)))]);
        using var tilemap = Create(_gba);

        Assert.Equal(0, tilemap.MostUsedPaletteSlot);
    }

    [Fact]
    public void MostUsedPaletteSlot_PicksMajoritySlot()
    {
        WriteRom([.. Enumerable.Range(0, Width * Height).Select(i => (i % Width, i / Width, Gba(1, palette: i < 20 ? 1 : 0)))]);
        using var tilemap = Create(_gba);

        Assert.Equal(1, tilemap.MostUsedPaletteSlot);
    }

    [Fact]
    public void TileCount_PastSource_IsZero()
    {
        WriteRom();
        using var tilemap = Create(_gba, tileOffset: _rom.Length + 100);

        Assert.Equal(0, tilemap.TileCount);
    }

    [Fact]
    public void Generate_LaysOutTilesFromFirstTile_InSlotPalette()
    {
        WriteRom();
        using var tilemap = Create(_gba, firstTile: 2);

        var palette = TilePalette.Generate(tilemap, 1);

        Assert.Equal(16, palette.Width);
        Assert.Equal(1, palette.Height);
        Assert.Equal(2, palette.GetTileIndex(0, 0));
        Assert.Equal(17, palette.GetTileIndex(15, 0));
        Assert.Equal(TileOffset + 3 * TileBytes, palette.Arranger.GetElement(3, 0)!.Value.SourceAddress.ByteOffset);
        Assert.Same(_slot1, ((IIndexedCodec)palette.Arranger.GetElement(3, 0)!.Value.Codec).Palette);

        var narrow = TilePalette.Generate(tilemap, 0, columns: 4);
        Assert.Equal(4, narrow.Height);
        Assert.Equal(2 + 9, narrow.GetTileIndex(1, 2));
    }

    private ScatteredArranger CreateCurated(MemoryDataSource other)
    {
        var curated = new ScatteredArranger("curated", PixelColorType.Indexed, ElementLayout.Tiled, 2, 2, 8, 8);
        curated.SetElement(TileElement(5, _unslotted).WithMirror(MirrorOperation.Horizontal), 0, 0);
        curated.SetElement(TileElement(5, _slot0, source: other), 1, 0);
        curated.SetElement(TileElement(5, _slot0, codecName: "SNES 4bpp"), 0, 1);
        curated.SetElement(TileElement(5, _slot0, byteOffset: 4), 1, 1);
        return curated;
    }

    [Fact]
    public void FromArranger_KeepsValidCells_RefusesForeignOtherCodecAndUnalignedCells()
    {
        WriteRom();
        using var tilemap = Create(_gba);
        using var other = new MemoryDataSource("other", 2048);

        var palette = TilePalette.FromArranger(tilemap, CreateCurated(other), 0);
        Assert.Equal(5, palette.GetTileIndex(0, 0));
        Assert.Null(palette.GetRefusal(0, 0));
        Assert.Same(_slot0, ((IIndexedCodec)palette.Arranger.GetElement(0, 0)!.Value.Codec).Palette);
        Assert.Equal(MirrorOperation.None, palette.Arranger.GetElement(0, 0)!.Value.Mirror);

        Assert.Equal(3, palette.RefusedCount);
        Assert.Null(palette.GetTileIndex(1, 0));
        Assert.Equal("reads data file 'other', not the tile set's data file 'rom'", palette.GetRefusal(1, 0));
        Assert.Equal("uses codec 'SNES 4bpp', not the tile set's codec 'GBA 4bpp'", palette.GetRefusal(0, 1));
        Assert.Contains("not on a tile boundary", palette.GetRefusal(1, 1));
    }

    [Fact]
    public void FromArranger_OtherElementSize_RefusesEveryCell()
    {
        WriteRom();
        using var tilemap = Create(_gba);
        var curated = new ScatteredArranger("curated", PixelColorType.Indexed, ElementLayout.Tiled, 2, 1, 16, 16);
        curated.SetElement(TileElement(1, _slot0), 0, 0);
        curated.SetElement(TileElement(2, _slot0), 1, 0);

        var palette = TilePalette.FromArranger(tilemap, curated, 0);

        Assert.Equal(2, palette.RefusedCount);
        Assert.Null(palette.GetTileIndex(0, 0));
        Assert.Contains("16x16", palette.GetRefusal(1, 0));
    }

    [Fact]
    public void CreateBrush_FromPaletteRectangle_SkipsRefusedCells()
    {
        WriteRom();
        using var tilemap = Create(_gba);
        using var other = new MemoryDataSource("other", 2048);
        var palette = TilePalette.FromArranger(tilemap, CreateCurated(other), 0);

        var brush = palette.CreateBrush(new Rectangle(0, 0, 2, 2), 1)!;

        Assert.Equal(2, brush.Width);
        Assert.Equal(2, brush.Height);
        Assert.Equal(1, brush.Slot);
        Assert.Equal(new TileBrushCell(5, MirrorOperation.None), brush.GetCell(0, 0));
        Assert.Null(brush.GetCell(1, 0));
        Assert.Null(brush.GetCell(0, 1));
        Assert.Null(brush.GetCell(1, 1));
        Assert.Null(palette.CreateBrush(new Rectangle(1, 0, 1, 2), 0));
    }

    [Fact]
    public void FlippedBrush_MirrorsLayoutAndEachCell()
    {
        var cells = new TileBrushCell?[,]
        {
            { new(1, MirrorOperation.None), new(2, MirrorOperation.Horizontal) },
            { new(3, MirrorOperation.Vertical), new(4, MirrorOperation.Both) }
        };
        var brush = new TileBrush(cells);

        var h = brush.WithFlips(true, false);
        Assert.Equal(new TileBrushCell(2, MirrorOperation.None), h.GetEffectiveCell(0, 0));
        Assert.Equal(new TileBrushCell(1, MirrorOperation.Horizontal), h.GetEffectiveCell(1, 0));
        Assert.Equal(new TileBrushCell(4, MirrorOperation.Vertical), h.GetEffectiveCell(0, 1));
        Assert.Equal(new TileBrushCell(3, MirrorOperation.Both), h.GetEffectiveCell(1, 1));

        var v = brush.WithFlips(false, true);
        Assert.Equal(new TileBrushCell(3, MirrorOperation.None), v.GetEffectiveCell(0, 0));
        Assert.Equal(new TileBrushCell(4, MirrorOperation.Horizontal), v.GetEffectiveCell(1, 0));
        Assert.Equal(new TileBrushCell(1, MirrorOperation.Vertical), v.GetEffectiveCell(0, 1));
        Assert.Equal(new TileBrushCell(2, MirrorOperation.Both), v.GetEffectiveCell(1, 1));

        var both = brush.WithFlips(true, true);
        Assert.Equal(new TileBrushCell(4, MirrorOperation.None), both.GetEffectiveCell(0, 0));
        Assert.Equal(new TileBrushCell(3, MirrorOperation.Horizontal), both.GetEffectiveCell(1, 0));
        Assert.Equal(new TileBrushCell(2, MirrorOperation.Vertical), both.GetEffectiveCell(0, 1));
        Assert.Equal(new TileBrushCell(1, MirrorOperation.Both), both.GetEffectiveCell(1, 1));
    }

    [Fact]
    public void Stamp_ThenWriteEntries_ChangesOnlyTargetEntry()
    {
        WriteRom();
        using var tilemap = Create(_gba);
        var before = ReadRom();
        var working = tilemap.CreateGrid();
        var plotter = new TilemapPlotter(tilemap, working);

        Assert.True(plotter.Stamp(TileBrush.Single(9, slot: 1, horizontalFlip: true), 2, 1));

        Assert.True(tilemap.WriteEntries(working).HasSucceeded);
        Assert.Equal(WithEntry(before, 2, 1, Gba(9, palette: 1, hflip: true)), ReadRom());
    }

    [Fact]
    public void Stamp_AtMapEdge_IsClipped()
    {
        WriteRom();
        using var tilemap = Create(_gba);
        var before = ReadRom();
        var working = tilemap.CreateGrid();
        var plotter = new TilemapPlotter(tilemap, working);

        Assert.True(plotter.Stamp(Brush(2, 2, 9, 10, 11, 12), Width - 1, Height - 1));
        Assert.True(plotter.Stamp(Brush(2, 2, 9, 10, 11, 12), -1, -1));

        Assert.True(tilemap.WriteEntries(working).HasSucceeded);
        Assert.Equal(WithEntry(WithEntry(before, Width - 1, Height - 1, Gba(9)), 0, 0, Gba(12)), ReadRom());
    }

    [Fact]
    public void Stamp_SameTileAgain_ReportsNoChange()
    {
        WriteRom();
        using var tilemap = Create(_gba);
        var working = tilemap.CreateGrid();
        var plotter = new TilemapPlotter(tilemap, working);

        Assert.False(plotter.Stamp(TileBrush.Single(1), 1, 0));
        Assert.True(plotter.Stamp(TileBrush.Single(9), 1, 0));
        Assert.False(plotter.Stamp(TileBrush.Single(9), 1, 0));
        Assert.True(plotter.Stamp(TileBrush.Single(9, slot: 1), 1, 0));
    }

    [Fact]
    public void Stamp_SlotSharingPalette_WritesBrushSlot()
    {
        WriteRom();
        using var tilemap = new TilemapArranger("tilemap", _rom, MapOffset, _gba, MapScreenLayout.Linear, Width, Height, _rom, TileOffset,
            "GBA 4bpp", 0, [_slot0, _slot0], _codecFactory);
        var before = ReadRom();
        var working = tilemap.CreateGrid();
        var plotter = new TilemapPlotter(tilemap, working);

        Assert.True(plotter.Stamp(TileBrush.Single(1, slot: 1), 1, 0));
        Assert.False(plotter.Stamp(TileBrush.Single(1, slot: 1), 1, 0));
        Assert.True(plotter.Stamp(TileBrush.Single(7, slot: 1), 2, 0));
        Assert.Equal(1, plotter.Pick(new Rectangle(2, 0, 1, 1))!.Slot);

        Assert.True(tilemap.WriteEntries(working).HasSucceeded);
        Assert.Equal(WithEntry(WithEntry(before, 1, 0, Gba(1, palette: 1)), 2, 0, Gba(7, palette: 1)), ReadRom());
    }

    [Fact]
    public void StampStroke_AlignsBrushToStrokeOrigin()
    {
        WriteRom();
        using var tilemap = Create(_gba);
        var working = tilemap.CreateGrid();
        var plotter = new TilemapPlotter(tilemap, working);
        var brush = Brush(2, 2, 9, 10, 11, 12);
        var origin = new Point(1, 1);

        bool StampStroke(Point cell)
        {
            var snapped = TilemapPlotter.SnapToStroke(brush, origin, cell);
            return plotter.Stamp(brush, snapped.X, snapped.Y);
        }

        Assert.True(StampStroke(origin));
        Assert.True(StampStroke(new Point(4, 2)));
        Assert.False(StampStroke(new Point(4, 1)));

        Assert.Equal(9, TileAt(tilemap, working, 1, 1));
        Assert.Equal(12, TileAt(tilemap, working, 2, 2));
        Assert.Equal(9, TileAt(tilemap, working, 3, 1));
        Assert.Equal(12, TileAt(tilemap, working, 4, 2));
        Assert.Equal((3 * Width + 5) % TileCount, TileAt(tilemap, working, 5, 3));
    }

    [Fact]
    public void Fill_TilesBrushOverRectangle()
    {
        WriteRom();
        using var tilemap = Create(_gba);
        var working = tilemap.CreateGrid();
        var plotter = new TilemapPlotter(tilemap, working);

        Assert.True(plotter.Fill(Brush(2, 1, 9, 10), new Rectangle(1, 1, 5, 3)));

        for (int y = 1; y < 4; y++)
        {
            for (int x = 1; x < 6; x++)
                Assert.Equal((x - 1) % 2 == 0 ? 9 : 10, TileAt(tilemap, working, x, y));
        }

        Assert.Equal(0, TileAt(tilemap, working, 0, 0));
        Assert.Equal((1 * Width + 6) % TileCount, TileAt(tilemap, working, 6, 1));
    }

    [Fact]
    public void Pick_SingleCell_GivesTileSlotAndFlips()
    {
        WriteRom((1, 0, Gba(5, palette: 1, hflip: true, vflip: true)));
        using var tilemap = Create(_gba);
        var working = tilemap.CreateGrid();
        var plotter = new TilemapPlotter(tilemap, working);

        var brush = plotter.Pick(new Rectangle(1, 0, 1, 1))!;

        Assert.Equal(new TileBrushCell(5, MirrorOperation.None), brush.GetCell(0, 0));
        Assert.Equal(1, brush.Slot);
        Assert.True(brush.HorizontalFlip);
        Assert.True(brush.VerticalFlip);
    }

    [Fact]
    public void Pick_SeveralCells_KeepsCellMirrorsAndFirstCellSlot()
    {
        WriteRom((0, 0, Gba(3, palette: 1, hflip: true)), (1, 0, Gba(4, vflip: true)), (2, 0, Gba(999)));
        using var tilemap = Create(_gba);
        var working = tilemap.CreateGrid();
        var plotter = new TilemapPlotter(tilemap, working);

        var brush = plotter.Pick(new Rectangle(0, 0, 3, 1))!;

        Assert.Equal(3, brush.Width);
        Assert.Equal(new TileBrushCell(3, MirrorOperation.Horizontal), brush.GetCell(0, 0));
        Assert.Equal(new TileBrushCell(4, MirrorOperation.Vertical), brush.GetCell(1, 0));
        Assert.Null(brush.GetCell(2, 0));
        Assert.Equal(1, brush.Slot);
        Assert.False(brush.HorizontalFlip);
        Assert.False(brush.VerticalFlip);
    }

    [Fact]
    public void Erase_ThenWriteEntries_WritesEmptyEntry()
    {
        WriteRom();
        using var tilemap = Create(_gba, emptyTile: 7);
        var before = ReadRom();
        var working = tilemap.CreateGrid();
        var plotter = new TilemapPlotter(tilemap, working);

        Assert.True(plotter.Erase(new Rectangle(2, 1, 1, 1)));
        Assert.False(plotter.Erase(new Rectangle(2, 1, 1, 1)));

        Assert.True(tilemap.WriteEntries(working).HasSucceeded);
        Assert.Equal(WithEntry(before, 2, 1, Gba(7)), ReadRom());
    }

    [Fact]
    public void FlipWithoutFormatField_IsRefused()
    {
        WriteRom();
        using var tilemap = Create(_mode7);
        var working = tilemap.CreateGrid();
        var plotter = new TilemapPlotter(tilemap, working);
        var brush = TileBrush.Single(9, horizontalFlip: true);
        var entry = working[0, 0];

        Assert.Equal("Map format 'SNES Mode 7' has no horizontal flip field", plotter.FindBrushError(brush));
        Assert.False(plotter.Stamp(brush, 0, 0));
        Assert.Equal(entry, working[0, 0]);
    }

    [Fact]
    public void TileOutsideTileSet_IsRefused()
    {
        WriteRom();
        using var tilemap = Create(_gba);
        var plotter = new TilemapPlotter(tilemap, tilemap.CreateGrid());

        Assert.Equal("Tile 16 is not in the tile set (tiles 0 to 15)", plotter.FindBrushError(TileBrush.Single(16)));
        Assert.False(plotter.Stamp(TileBrush.Single(16), 0, 0));
    }
}
