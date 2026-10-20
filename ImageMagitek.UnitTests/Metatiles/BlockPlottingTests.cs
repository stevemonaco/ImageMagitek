using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
using ImageMagitek.Metatiles;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using ImageMagitek.UnitTests.Tilemaps;
using Xunit;

namespace ImageMagitek.UnitTests.Metatiles;

public sealed class BlockPlottingTests : IDisposable
{
    private static readonly MapFormatFactory _formats = MapFormatTests.LoadShippedFormats();
    private static readonly MapFormat _gba = _formats.Find("GBA Text BG")!;
    private static readonly MapFormat _frlg = _formats.Find("FRLG Block")!;
    private static readonly byte[] _sentinel = [0xAA, 0xAA, 0xAA, 0xAA];

    private readonly ICodecFactory _codecFactory = CodecFixture.Shared.CodecFactory;
    private readonly Palette _palette = ArrangerTestFactory.CreatePalette(Enumerable.Range(0, 16).Select(i => new ColorRgba32((byte)i, 0, 0, 255)).ToArray());
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();
    }

    private MemoryDataSource Source(string name, byte[] bytes)
    {
        var source = new MemoryDataSource(name);
        _disposables.Add(source);
        source.Write(bytes);
        return source;
    }

    private static byte[] Blocks(params int[] values) => values.SelectMany(x => new[] { (byte)x, (byte)(x >> 8) }).ToArray();

    /// <summary>
    /// A one-layer set of 1x1-tile metatiles, metatile m drawing tile m
    /// </summary>
    private MetatileSet Set(string name, int count, int firstTile = 0, bool assignsSlot0 = true) =>
        new(name, Source($"{name} table", Blocks(Enumerable.Range(firstTile, count).ToArray())), 0, count, new Size(1, 1), 1, _gba, MetatileOrder.Interleaved,
            Source($"{name} tiles", new byte[count * 32]), 0, "GBA 4bpp", firstTile, count, assignsSlot0 ? [_palette] : [], _codecFactory);

    private MetatileMap Map(DataSource blocks, int width, int height, long offset = 0, int emptyBlock = 0, params MetatileMapSet[] sets)
    {
        var map = new MetatileMap("Map", blocks, offset, _frlg, MapScreenLayout.Linear, width, height,
            sets.Length > 0 ? sets : [new MetatileMapSet(Set("Set", 4), 0)], emptyBlock);
        _disposables.Add(map);
        return map;
    }

    private static int?[] Numbers(BlockGrid grid) =>
        Enumerable.Range(0, grid.Height).SelectMany(y => Enumerable.Range(0, grid.Width).Select(x => grid[x, y])).ToArray();

    [Fact]
    public void BlockGrid_ReadsBlockNumbers()
    {
        var map = Map(Source("blocks", Blocks(0x3401, 0x0802, 0xF403, 0x0400)), 2, 2);
        var truncated = Map(Source("short", Blocks(1, 2, 3)), 2, 2);

        var grid = map.CreateBlockGrid();
        var truncatedGrid = truncated.CreateBlockGrid();

        Assert.Equal((2, 2), (grid.Width, grid.Height));
        Assert.Equal([1, 2, 3, 0], Numbers(grid));
        Assert.Equal([1, 2, 3, null], Numbers(truncatedGrid));
        Assert.False(truncatedGrid.TrySet(1, 1, 2));
        Assert.False(grid.TrySet(0, 0, 1));

        var clone = grid.Clone();
        Assert.True(grid.TrySet(0, 0, 2));
        Assert.Equal(1, clone[0, 0]);
    }

    [Fact]
    public void Write_ChangesOnlyTargetIndexBits_KeepsBlockBits()
    {
        var blocks = Source("blocks", [.. _sentinel, .. Blocks(0x3401, 0x0802, 0xF403, 0xFC00), .. _sentinel]);
        var map = Map(blocks, 2, 2, offset: 4);
        var writes = 0;
        blocks.DataWritten += (_, _) => writes++;

        var grid = map.CreateBlockGrid();
        Assert.True(new MetatileMapPlotter(map, grid).Stamp(BlockBrush.Single(3), 1, 1));
        var result = map.WriteBlocks(grid);

        Assert.True(result.HasSucceeded);
        Assert.Equal([.. _sentinel, .. Blocks(0x3401, 0x0802, 0xF403, 0xFC03), .. _sentinel], blocks.Read(new BitAddress(0, 0), 16 * 8));
        Assert.Equal(1, writes);
        Assert.Equal(0x3F, map.BlockBitsValue(1, 1));
        Assert.Equal(3, map.CreateBlockGrid()[1, 1]);
    }

    [Theory]
    [InlineData(9, "in no metatile set")]
    [InlineData(1024, "does not fit the 10-bit index field")]
    public void Write_NumberOutsideSets_FailsAndWritesNothing(int number, string reason)
    {
        var bytes = Blocks(0x3401, 0x0802);
        var blocks = Source("blocks", bytes);
        var map = Map(blocks, 2, 1);
        var writes = 0;
        blocks.DataWritten += (_, _) => writes++;

        var grid = map.CreateBlockGrid();
        grid.TrySet(0, 0, 3);
        grid.TrySet(1, 0, number);
        var result = map.WriteBlocks(grid);

        Assert.True(result.HasFailed);
        Assert.Contains("Block (1, 0)", result.AsError.Reason);
        Assert.Contains(number.ToString(), result.AsError.Reason);
        Assert.Contains(reason, result.AsError.Reason);
        Assert.Equal(bytes, blocks.Read(new BitAddress(0, 0), 4 * 8));
        Assert.Equal(0, writes);
    }

    [Fact]
    public void Write_NoChange_WritesNothing()
    {
        var blocks = Source("blocks", Blocks(1, 2));
        var map = Map(blocks, 2, 1);
        var writes = 0;
        blocks.DataWritten += (_, _) => writes++;

        Assert.True(map.WriteBlocks(map.CreateBlockGrid()).HasSucceeded);
        Assert.Equal(0, writes);
    }

    [Fact]
    public void CompressedBlockMap_IsReadOnly()
    {
        // At offset 4: a GBA LZ77 block decompressing to "ABABABAB", four blocks numbered 0x241
        var parent = Source("parent", [0xEE, 0xEE, 0xEE, 0xEE, 0x10, 0x08, 0x00, 0x00, 0x20, 0x41, 0x42, 0x30, 0x01, 0xEE]);
        var compressed = new CompressedDataSource("compressed", parent, 4, "GBA LZ77", new CompressorFactory());
        _disposables.Add(compressed);
        var map = Map(compressed, 4, 1, sets: [new MetatileMapSet(Set("Set", 600), 0)]);

        var grid = map.CreateBlockGrid();
        Assert.Equal(0x241, grid[0, 0]);
        grid.TrySet(0, 0, 1);
        var result = map.WriteBlocks(grid);

        Assert.Equal("reads compressed data 'compressed', which is read-only", map.BlockMapReadOnlyReason);
        Assert.True(result.HasFailed);
        Assert.Contains("read-only because it reads compressed data", result.AsError.Reason);
        Assert.Null(Map(Source("plain", Blocks(0)), 1, 1).BlockMapReadOnlyReason);
    }

    [Fact]
    public void CompressedBlockMap_NoChange_Succeeds()
    {
        var parent = Source("parent", [0xEE, 0xEE, 0xEE, 0xEE, 0x10, 0x08, 0x00, 0x00, 0x20, 0x41, 0x42, 0x30, 0x01, 0xEE]);
        var compressed = new CompressedDataSource("compressed", parent, 4, "GBA LZ77", new CompressorFactory());
        _disposables.Add(compressed);
        var map = Map(compressed, 4, 1, sets: [new MetatileMapSet(Set("Set", 600), 0)]);

        Assert.True(map.WriteBlocks(map.CreateBlockGrid()).HasSucceeded);
    }

    [Fact]
    public void Stamp_ClipsAtEdge()
    {
        var map = Map(Source("blocks", Blocks(0, 0, 0, 0)), 2, 2);
        var grid = map.CreateBlockGrid();
        var brush = new BlockBrush(new int?[,] { { 1, 2 }, { null, 3 } });

        Assert.True(new MetatileMapPlotter(map, grid).Stamp(brush, 1, 0));

        Assert.Equal([0, 1, 0, 0], Numbers(grid));
    }

    [Fact]
    public void Stamp_NoChange_ReportsFalse()
    {
        var map = Map(Source("blocks", Blocks(1, 0)), 2, 1);
        var grid = map.CreateBlockGrid();
        var plotter = new MetatileMapPlotter(map, grid);

        Assert.False(plotter.Stamp(BlockBrush.Single(1), 0, 0));
        Assert.False(plotter.Stamp(BlockBrush.Single(9), 1, 0));
        Assert.Equal("Block 9 is in no metatile set of map 'Map'", plotter.FindBrushError(BlockBrush.Single(9)));
        Assert.Equal([1, 0], Numbers(grid));
    }

    [Fact]
    public void Stroke_AlignsToFirstBlock()
    {
        var map = Map(Source("blocks", Blocks(new int[5])), 5, 1);
        var grid = map.CreateBlockGrid();
        var plotter = new MetatileMapPlotter(map, grid);
        var brush = new BlockBrush(new int?[,] { { 1, 2 } });
        var origin = new Point(1, 0);

        foreach (var x in new[] { 1, 2, 3, 4 })
        {
            var snapped = BrushStroke.SnapToStroke(brush.Size, origin, new Point(x, 0));
            plotter.Stamp(brush, snapped.X, snapped.Y);
        }

        Assert.Equal(new Point(-1, 0), BrushStroke.SnapToStroke(brush.Size, origin, new Point(0, 0)));
        Assert.Equal([0, 1, 2, 1, 2], Numbers(grid));
    }

    [Fact]
    public void Fill_TilesBrush()
    {
        var map = Map(Source("blocks", Blocks(new int[9])), 3, 3);
        var grid = map.CreateBlockGrid();
        var brush = new BlockBrush(new int?[,] { { 1, 2 }, { 3, null } });

        Assert.True(new MetatileMapPlotter(map, grid).Fill(brush, new Rectangle(0, 0, 3, 2)));

        Assert.Equal([1, 2, 1, 3, 0, 3, 0, 0, 0], Numbers(grid));
    }

    [Fact]
    public void Pick_Rectangle()
    {
        var map = Map(Source("blocks", Blocks(1, 9, 2, 3)), 2, 2);
        var truncated = Map(Source("short", Blocks(2)), 2, 1);
        var plotter = new MetatileMapPlotter(map, map.CreateBlockGrid());

        var brush = plotter.Pick(new Rectangle(0, 0, 3, 3))!;
        var single = plotter.Pick(new Rectangle(1, 1, 1, 1))!;

        Assert.Equal((2, 2), (brush.Width, brush.Height));
        Assert.Equal([1, null, 2, 3], new[] { brush.GetCell(0, 0), brush.GetCell(1, 0), brush.GetCell(0, 1), brush.GetCell(1, 1) });
        Assert.Equal(3, single.GetCell(0, 0));
        Assert.Null(plotter.Pick(new Rectangle(1, 0, 1, 1)));
        Assert.Null(new MetatileMapPlotter(truncated, truncated.CreateBlockGrid()).Pick(new Rectangle(1, 0, 1, 1)));
    }

    [Fact]
    public void Erase_WritesEmptyBlock()
    {
        var blocks = Source("blocks", Blocks(0x0402, 3));
        var map = Map(blocks, 2, 1, emptyBlock: 1);
        var grid = map.CreateBlockGrid();

        Assert.True(new MetatileMapPlotter(map, grid).Erase(new Rectangle(0, 0, 2, 1)));
        Assert.True(map.WriteBlocks(grid).HasSucceeded);

        Assert.Equal(Blocks(0x0401, 1), blocks.Read(new BitAddress(0, 0), 4 * 8));
    }

    [Fact]
    public void Erase_EmptyBlockOutsideSets_Writes()
    {
        var blocks = Source("blocks", Blocks(512));
        var map = Map(blocks, 1, 1, sets: [new MetatileMapSet(Set("Set", 4), 512)]);
        var grid = map.CreateBlockGrid();

        Assert.True(new MetatileMapPlotter(map, grid).Erase(new Rectangle(0, 0, 1, 1)));
        Assert.True(map.WriteBlocks(grid).HasSucceeded);

        Assert.Equal(Blocks(0), blocks.Read(new BitAddress(0, 0), 2 * 8));
    }

    [Fact]
    public void BlockPalette_LaysOutSetsOnNewRows()
    {
        var map = Map(Source("blocks", Blocks(0)), 1, 1, sets: [new MetatileMapSet(Set("Primary", 10), 0), new MetatileMapSet(Set("Secondary", 3, firstTile: 10, assignsSlot0: false), 640)]);

        var palette = BlockPalette.Generate(map);
        var brush = palette.CreateBrush(new Rectangle(1, 1, 2, 3))!;

        Assert.Equal((8, 3), (palette.Width, palette.Height));
        Assert.Equal(7, palette.GetBlockNumber(7, 0));
        Assert.Equal(9, palette.GetBlockNumber(1, 1));
        Assert.Null(palette.GetBlockNumber(2, 1));
        Assert.Equal(640, palette.GetBlockNumber(0, 2));
        Assert.Equal(642, palette.GetBlockNumber(2, 2));
        Assert.Null(palette.GetBlockNumber(3, 2));
        Assert.Equal(new Point(1, 2), palette.FindCell(641));
        Assert.Equal((2, 2), (brush.Width, brush.Height));
        Assert.Equal([9, null, 641, 642], new[] { brush.GetCell(0, 0), brush.GetCell(1, 0), brush.GetCell(0, 1), brush.GetCell(1, 1) });
        Assert.Equal((64, 24), (palette.Render().Width, palette.Render().Height));
    }
}
