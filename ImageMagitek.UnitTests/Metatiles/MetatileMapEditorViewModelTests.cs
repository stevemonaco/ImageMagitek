using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
using ImageMagitek.Metatiles;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using ImageMagitek.UnitTests.Tilemaps;
using SkiaSharp;
using TileShop.Shared.Interactions;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Metatiles;

public sealed class MetatileMapEditorViewModelTests : IDisposable
{
    private static readonly MapFormatFactory _formats = MapFormatTests.LoadShippedFormats();
    private static readonly byte[] _sentinel = [0xAA, 0xAA, 0xAA, 0xAA];

    private readonly Palette _palette = ArrangerTestFactory.CreatePalette(Enumerable.Range(0, 16).Select(i => new ColorRgba32((byte)(i * 16), 0, 0, 255)).ToArray());
    private readonly RecordingInteractionService _interactions = new();
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

    private MemoryDataSource Source(string name, byte[] bytes)
    {
        var source = Track(new MemoryDataSource(name));
        source.Write(bytes);
        return source;
    }

    private static byte[] Blocks(params int[] values) => values.SelectMany(x => new[] { (byte)x, (byte)(x >> 8) }).ToArray();

    /// <summary>
    /// A one-layer set of four 1x1-tile metatiles, metatile m drawing tile m, with 4-byte attributes 0x100 + m
    /// </summary>
    private MetatileSet Set() =>
        new("Set", Source("table", Blocks(0, 1, 2, 3)), 0, 4, new Size(1, 1), 1, _formats.Find("GBA Text BG")!, MetatileOrder.Interleaved,
            Source("tiles", Enumerable.Range(0, 4).SelectMany(i => Enumerable.Repeat((byte)(i * 0x11), 32)).ToArray()), 0, "GBA 4bpp", 0, 4,
            [_palette], CodecFixture.Shared.CodecFactory,
            Source("attributes", Enumerable.Range(0, 4).SelectMany(i => new byte[] { (byte)i, 1, 0, 0 }).ToArray()), 0, 4);

    private MetatileMap Map(DataSource blocks, int width, int height, long offset = 0) =>
        Track(new MetatileMap("Map", blocks, offset, _formats.Find("FRLG Block")!, MapScreenLayout.Linear, width, height, [new MetatileMapSet(Set(), 0)]));

    private MetatileMapEditorViewModel Editor(MetatileMap map) => new(map, new GridPreferences(), _interactions, _ => Task.CompletedTask);

    private static MetatileMapEditorViewModel InBlocksMode(MetatileMapEditorViewModel editor)
    {
        editor.ChangeEditModeCommand.Execute(MetatileEditMode.Blocks);
        Assert.True(editor.IsBlocksMode);
        return editor;
    }

    private static int?[] Numbers(BlockGrid grid) =>
        Enumerable.Range(0, grid.Height).SelectMany(y => Enumerable.Range(0, grid.Width).Select(x => grid[x, y])).ToArray();

    private static void Stroke(MetatileMapEditorViewModel editor, string name, Action plot)
    {
        editor.BeginCellStroke(name);
        plot();
        editor.EndCellStroke();
    }

    [Fact]
    public void HidingLayer_RedrawsWithoutIt()
    {
        var palette = new Palette("zero transparent", new ColorFactory(), ColorModel.Rgba32, true, PaletteStorageSource.GlobalJson);
        palette.SetColorSources(new[] { new ColorRgba32(0, 0, 0, 255), new ColorRgba32(255, 0, 0, 255), new ColorRgba32(0, 255, 0, 255) }
            .Select(x => new ProjectNativeColorSource(x)));

        var tiles = Source("tiles", [.. Enumerable.Repeat((byte)0x11, 32), .. Enumerable.Repeat((byte)0x22, 32)]);
        var table = Source("table", [0x00, 0x00, 0x01, 0x00]);
        var blocks = Source("blocks", [0x00, 0x00]);

        var set = new MetatileSet("Set", table, 0, 1, new Size(1, 1), 2, _formats.Find("GBA Text BG")!, MetatileOrder.Interleaved,
            tiles, 0, "GBA 4bpp", 0, null, [palette], CodecFixture.Shared.CodecFactory);
        var map = Track(new MetatileMap("Map", blocks, 0, _formats.Find("FRLG Block")!, MapScreenLayout.Linear, 1, 1,
            [new MetatileMapSet(set, 0)]));
        var editor = Editor(map);
        Assert.Equal(["Bottom", "Top"], editor.Layers.Select(x => x.Name));
        Assert.Equal(SKColors.Lime, editor.Bitmap.Bitmap.GetPixel(0, 0));

        editor.Layers[1].IsVisible = false;

        Assert.False(map.IsLayerVisible(1));
        Assert.Equal(SKColors.Red, editor.Bitmap.Bitmap.GetPixel(0, 0));
    }

    [Fact]
    public void ReadOnlyBlockMap_DisablesBlocksWithReason()
    {
        // At offset 4: a GBA LZ77 block decompressing to "ABABABAB"
        var parent = Source("parent", [0xEE, 0xEE, 0xEE, 0xEE, 0x10, 0x08, 0x00, 0x00, 0x20, 0x41, 0x42, 0x30, 0x01, 0xEE]);
        var compressed = Track(new CompressedDataSource("compressed", parent, 4, "GBA LZ77", new CompressorFactory()));
        var editor = Editor(Map(compressed, 4, 1));

        editor.ChangeEditModeCommand.Execute(MetatileEditMode.Blocks);

        Assert.False(editor.CanPlotBlocks);
        Assert.Equal("The block map is read-only because it reads compressed data 'compressed', which is read-only", editor.BlocksDisabledReason);
        Assert.Equal(MetatileEditMode.View, editor.EditMode);
        Assert.True(Editor(Map(Source("blocks", Blocks(0)), 1, 1)).CanPlotBlocks);
    }

    [Fact]
    public void PlotWithoutBrush_ShowsMessage()
    {
        var editor = InBlocksMode(Editor(Map(Source("blocks", Blocks(0, 0)), 2, 1)));
        var messages = new List<string>();
        var recipient = new object();
        WeakReferenceMessenger.Default.Register<NotifyStatusMessage>(recipient, (_, m) =>
        {
            if (m.NotifyMessage == "Pick a block from the block palette")
                messages.Add(m.NotifyMessage);
        });

        try
        {
            Stroke(editor, "Plot Blocks", () =>
            {
                editor.PlotStroke(new Point(0, 0));
                editor.PlotStroke(new Point(1, 0));
            });
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }

        Assert.Single(messages);
        Assert.Equal([0, 0], Numbers(editor.Grid));
        Assert.False(editor.IsModified);
        Assert.Empty(editor.UndoHistory);
    }

    [Fact]
    public void PlotBlocks_UndoRedo()
    {
        var editor = InBlocksMode(Editor(Map(Source("blocks", Blocks(1, 1)), 2, 1)));

        editor.BlockPalette!.BrushBlockNumber = 2;
        Assert.Equal("Brush: block 2", editor.BlockPalette.BrushDescription);
        Stroke(editor, "Plot Blocks", () => editor.PlotStroke(new Point(0, 0)));
        editor.BlockPalette.BrushBlockNumber = 3;
        editor.FillBlocks(new Rectangle(0, 0, 2, 1));
        Stroke(editor, "Erase Blocks", () => editor.EraseStroke(new Point(1, 0)));

        Assert.Equal(["Plot Blocks", "Fill Blocks", "Erase Blocks"], editor.UndoHistory.Select(x => x.Name));
        Assert.Equal([3, 0], Numbers(editor.Grid));
        Assert.True(editor.IsModified);

        editor.Undo();
        Assert.Equal([3, 3], Numbers(editor.Grid));
        editor.Undo();
        Assert.Equal([2, 1], Numbers(editor.Grid));
        Assert.True(editor.IsModified);
        editor.Undo();
        Assert.Equal([1, 1], Numbers(editor.Grid));
        Assert.False(editor.IsModified);
        Assert.False(editor.CanUndo);

        editor.Redo();
        Assert.Equal([2, 1], Numbers(editor.Grid));
        Assert.True(editor.IsModified);
        editor.Redo();
        editor.Redo();
        Assert.Equal([3, 0], Numbers(editor.Grid));
        Assert.False(editor.CanRedo);
    }

    [Fact]
    public async Task Save_WritesChangedBlocksOnly()
    {
        var blocks = Source("blocks", [.. _sentinel, .. Blocks(0x3401, 0xFC02), .. _sentinel]);
        var editor = InBlocksMode(Editor(Map(blocks, 2, 1, offset: 4)));

        editor.BlockPalette!.BrushBlockNumber = 3;
        Stroke(editor, "Plot Blocks", () => editor.PlotStroke(new Point(1, 0)));
        await editor.SaveChangesAsync();

        Assert.Equal([.. _sentinel, .. Blocks(0x3401, 0xFC03), .. _sentinel], blocks.Read(new BitAddress(0, 0), 12 * 8));
        Assert.False(editor.IsModified);
        Assert.Empty(editor.UndoHistory);
        Assert.Empty(_interactions.Alerts);
    }

    [Fact]
    public async Task Save_Failure_AlertsAndKeepsEdits()
    {
        var path = TestPaths.CreateTempPath(".bin");
        File.WriteAllBytes(path, Blocks(0, 0));

        try
        {
            var source = Track(new FileDataSource("blocks.bin", path));
            var editor = InBlocksMode(Editor(Map(source, 2, 1)));
            editor.BlockPalette!.BrushBlockNumber = 2;
            Stroke(editor, "Plot Blocks", () => editor.PlotStroke(new Point(0, 0)));

            File.SetAttributes(path, FileAttributes.ReadOnly);
            source.Reopen();
            await editor.SaveChangesAsync();

            var (heading, message) = Assert.Single(_interactions.Alerts);
            Assert.Equal("Save Error", heading);
            Assert.Contains("read-only because it reads data file 'blocks.bin'", message);
            Assert.True(editor.IsModified);
            Assert.Equal([2, 0], Numbers(editor.Grid));
            Assert.Single(editor.UndoHistory);
            Assert.Equal(Blocks(0, 0), File.ReadAllBytes(path));
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
            foreach (var disposable in _disposables)
                disposable.Dispose();
            _disposables.Clear();
            File.Delete(path);
        }
    }

    [Fact]
    public void Hover_DescribesBlock()
    {
        var editor = Editor(Map(Source("blocks", Blocks(0x3401, 9, 0)), 2, 2));

        editor.UpdateActivityMessage(9, 2);
        Assert.Equal("(1, 0)  Block 9  ·  Bits 0", editor.ActivityMessage);

        editor.UpdateActivityMessage(3, 7);
        Assert.Equal("(0, 0)  Block 1  ·  Set  ·  Bits 13  ·  Attribute 0x00000101", editor.ActivityMessage);

        editor.UpdateActivityMessage(8, 8);
        Assert.Equal("(1, 1)  No block data", editor.ActivityMessage);
    }

    private sealed class RecordingInteractionService : IInteractionService
    {
        public List<(string Heading, string Message)> Alerts { get; } = [];

        public Task AlertAsync(string heading, string message)
        {
            Alerts.Add((heading, message));
            return Task.CompletedTask;
        }

        public Task<PromptResult> PromptAsync(PromptChoice choices, string heading, string message) => Task.FromResult(PromptResult.Accept);

        public Task<TResult?> RequestAsync<TResult>(IRequestMediator<TResult> mediator) => throw new NotSupportedException();
    }
}
