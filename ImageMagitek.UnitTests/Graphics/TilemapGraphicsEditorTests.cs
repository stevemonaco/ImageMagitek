using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.Services.Stores;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using ImageMagitek.UnitTests.Tilemaps;
using Microsoft.Extensions.Logging.Abstractions;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;
using TileShop.Shared.Services;
using TileShop.UI.Models;
using TileShop.UI.Services;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Graphics;

/// <summary>
/// A graphics editor opened on a 4x1 GBA tilemap whose slots 1 and 2 share a palette
/// </summary>
public sealed class TilemapGraphicsEditorTests : IAsyncLifetime
{
    private const int TileBytes = 32;

    private readonly string _directory = TestPaths.CreateTempPath("");
    private readonly ColorFactory _colorFactory = new();
    private readonly Palette _globalPalette = ArrangerTestFactory.CreatePalette(new ColorRgba32(0, 0, 0, 255));
    private readonly MapFormatFactory _mapFormats = MapFormatTests.LoadShippedFormats();
    private readonly ProjectService _projectService;
    private readonly ScriptedInteractionService _interactions = new();
    private readonly UserPreferencesStore _preferences;
    private readonly EditorsViewModel _editors;
    private ProjectTree _tree = null!;
    private FileDataSource _map = null!;
    private FileDataSource _tiles = null!;
    private FileDataSource _other = null!;
    private Palette _pal0 = null!;
    private Palette _shared = null!;
    private Palette _unslotted = null!;

    private string MapFile => Path.Combine(_directory, "map.bin");

    public TilemapGraphicsEditorTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(MapFile, [.. Gba(1, 1), .. Gba(2, 2), .. Gba(3), .. Gba(1)]);
        File.WriteAllBytes(Path.Combine(_directory, "tiles.bin"), Enumerable.Range(0, 16 * TileBytes).Select(x => (byte)(x * 7)).ToArray());
        File.WriteAllBytes(Path.Combine(_directory, "other.bin"), new byte[16 * TileBytes]);

        var serializerFactory = new XmlProjectSerializerFactory(Path.Combine(AppContext.BaseDirectory, "_schemas", "ResourceSchema.xsd"),
            CodecFixture.Shared.CodecFactory, _colorFactory, [_globalPalette], null, _mapFormats);
        _projectService = new ProjectService(serializerFactory);

        _preferences = new UserPreferencesStore(Path.Combine(_directory, "preferences.json"), NullLogger<UserPreferencesStore>.Instance);
        _editors = new EditorsViewModel(SettingsService.CreateDefault(), _interactions, _preferences, CodecFixture.Shared.CodecService,
            _colorFactory, new PaletteStore(_globalPalette, [_globalPalette]), _projectService, new ElementStore(), NullLoggerFactory.Instance,
            new HotkeyService(), null!, new ClipboardService());
    }

    public async Task InitializeAsync()
    {
        _tree = (await _projectService.CreateNewProjectAsync(Path.Combine(_directory, "project.xml"))).AsSuccess.Result;
        _map = await AddAsync(new FileDataSource("Map", MapFile));
        _tiles = await AddAsync(new FileDataSource("Tiles", Path.Combine(_directory, "tiles.bin")));
        _other = await AddAsync(new FileDataSource("Other", Path.Combine(_directory, "other.bin")));
        _pal0 = await AddAsync(CreatePalette("Pal0", 10));
        _shared = await AddAsync(CreatePalette("Shared", 40));
        _unslotted = await AddAsync(CreatePalette("Unslotted", 80));
    }

    public Task DisposeAsync()
    {
        _projectService.CloseProjects();

        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }

        return Task.CompletedTask;
    }

    private static byte[] Gba(int tile, int palette = 0)
    {
        var value = tile | palette << 12;
        return [(byte)value, (byte)(value >> 8)];
    }

    private Palette CreatePalette(string name, byte seed) =>
        new(name, _colorFactory, ColorModel.Rgba32,
            Enumerable.Range(0, 16).Select(i => (IColorSource)new ProjectNativeColorSource(new ColorRgba32((byte)(seed + i), 0, 0, 255))).ToArray(),
            true, PaletteStorageSource.ProjectXml, _map);

    private async Task<T> AddAsync<T>(T resource) where T : IProjectResource
    {
        Assert.True((await _projectService.AddResourceAsync(_tree.Root, resource)).HasSucceeded);
        return resource;
    }

    private TilemapArranger CreateTilemap(DataSource map) =>
        new("Screen", map, 0, _mapFormats.Find("GBA Text BG")!, MapScreenLayout.Linear, 4, 1, _tiles, 0, "GBA 4bpp", 0,
            [_pal0, _shared, _shared], CodecFixture.Shared.CodecFactory);

    private async Task<(TilemapArranger Tilemap, GraphicsEditorViewModel Editor)> OpenAsync()
    {
        var tilemap = await AddAsync(CreateTilemap(_map));
        await _editors.ActivateEditor(tilemap);
        var editor = Assert.IsType<GraphicsEditorViewModel>(_editors.ActiveEditor);
        editor.EditMode = GraphicsEditMode.Arrange;
        return (tilemap, editor);
    }

    private static TilemapGrid Grid(GraphicsEditorViewModel editor) => Assert.IsType<TilemapView>(editor.WorkingArranger).Grid;

    private byte[] ReadMap() => _map.Read(BitAddress.Zero, 8 * 8);

    private async Task<ScatteredArranger> AddScatteredAsync(DataSource source, int tile, Palette palette)
    {
        var codec = (IIndexedCodec)CodecFixture.Shared.CodecFactory.CreateCodec("GBA 4bpp")!;
        codec.Palette = palette;
        var arranger = new ScatteredArranger("Source", PixelColorType.Indexed, ElementLayout.Tiled, 1, 1, 8, 8);
        arranger.SetElement(new ArrangerElement(0, 0, source, new BitAddress(tile * TileBytes, 0), codec), 0, 0);
        return await AddAsync(arranger);
    }

    private static List<string> CaptureStatus(Action action)
    {
        var messages = new List<string>();
        var recipient = new object();
        WeakReferenceMessenger.Default.Register<NotifyStatusMessage>(recipient, (_, m) => messages.Add(m.NotifyMessage));

        try
        {
            action();
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }

        return messages;
    }

    [Fact]
    public async Task Open_Tilemap_OffersArrangeAndTilesOnAWorkingView()
    {
        var (tilemap, editor) = await OpenAsync();

        Assert.True(editor.IsTilemap);
        Assert.True(editor.CanView && editor.CanArrange && editor.CanPlotTiles && editor.CanDraw);
        Assert.Same(tilemap, Assert.IsType<TilemapView>(editor.WorkingArranger).Tilemap);
        Assert.Equal("Tilemap entries cannot be rotated", editor.RotateDisabledReason);
        Assert.Null(editor.MirrorHorizontalDisabledReason);
    }

    [Fact]
    public async Task Paste_FromSameTilemap_KeepsSlot_UndoRedoAndSave()
    {
        var (_, editor) = await OpenAsync();
        var copy = editor.WorkingArranger.CopyElements(1, 0, 1, 1);

        editor.ApplyPaste(new ArrangerPaste(copy, SnapMode.Element));

        Assert.True(editor.IsModified);
        Assert.Equal(new MapEntry(2, 2, false, false, false), Grid(editor)[0, 0]);

        editor.Undo();
        Assert.Equal(new MapEntry(1, 1, false, false, false), Grid(editor)[0, 0]);
        editor.Redo();
        Assert.Equal(new MapEntry(2, 2, false, false, false), Grid(editor)[0, 0]);

        await editor.SaveChangesAsync();

        Assert.False(editor.IsModified);
        Assert.Equal(Gba(2, 2), ReadMap()[0..2]);
        Assert.Equal(new MapEntry(2, 2, false, false, false), Grid(editor)[0, 0]);
    }

    [Fact]
    public async Task Paste_ForeignElement_ShowsStatusAndChangesNothing()
    {
        var (_, editor) = await OpenAsync();
        var source = await AddScatteredAsync(_other, 0, _pal0);

        var messages = CaptureStatus(() => editor.ApplyPaste(new ArrangerPaste(source.CopyElements(), SnapMode.Element)));

        Assert.Contains("Cannot paste into tilemap 'Screen': element at (0, 0) reads data file 'Other', not the tile set's data file 'Tiles'", messages);
        Assert.False(editor.IsModified);
        Assert.Empty(editor.UndoHistory);
        Assert.Equal(new MapEntry(1, 1, false, false, false), Grid(editor)[0, 0]);
    }

    [Fact]
    public async Task Paste_PaletteInNoSlot_TakesDestinationSlot()
    {
        var (_, editor) = await OpenAsync();
        var source = await AddScatteredAsync(_tiles, 5, _unslotted);

        editor.ApplyPaste(new ArrangerPaste(source.CopyElements(), SnapMode.Element));

        Assert.Equal(new MapEntry(5, 1, false, false, false), Grid(editor)[0, 0]);
    }

    [Fact]
    public async Task ApplyPalette_PaletteInNoSlot_ShowsStatusAndChangesNothing()
    {
        var (_, editor) = await OpenAsync();

        var applied = true;
        var messages = CaptureStatus(() => applied = editor.TryApplyPalette(0, 0, _unslotted));

        Assert.False(applied);
        Assert.Contains("Palette 'Unslotted' is not in a palette slot of tilemap 'Screen'", messages);
        Assert.False(editor.IsModified);
    }

    [Fact]
    public async Task ApplyPalette_SlottedPalette_SetsSlotAndKeepsSharedSlot()
    {
        var (_, editor) = await OpenAsync();

        Assert.True(editor.TryApplyPalette(16, 0, _shared));
        Assert.False(editor.TryApplyPalette(8, 0, _shared));

        Assert.Equal(1, Grid(editor)[2, 0]!.Value.Palette);
        Assert.Equal(2, Grid(editor)[1, 0]!.Value.Palette);
        Assert.True(editor.IsModified);
    }

    [Fact]
    public async Task Delete_ThenSave_WritesEmptyEntries()
    {
        var (tilemap, editor) = await OpenAsync();
        editor.SelectAll();

        editor.DeleteElementSelection();

        Assert.All(Enumerable.Range(0, 4), x => Assert.Null(editor.WorkingArranger.GetElement(x, 0)));
        await editor.SaveChangesAsync();

        Assert.Equal(new byte[8], ReadMap());
        Assert.All(Enumerable.Range(0, 4), x => Assert.Equal(new MapEntry(0, 0, false, false, false), Grid(editor)[x, 0]));
        Assert.Equal(0, tilemap.GetElement(0, 0)!.Value.SourceAddress.ByteOffset);
    }

    [Fact]
    public async Task Mirror_EditsEntryFlips()
    {
        var (_, editor) = await OpenAsync();

        Assert.True(editor.TryMirrorElement(1, 0, MirrorOperation.Vertical).HasSucceeded);
        Assert.True(editor.TryRotateElement(1, 0, RotationOperation.Left).HasFailed);

        Assert.Equal(new MapEntry(2, 2, false, true, false), Grid(editor)[1, 0]);
    }

    [Fact]
    public async Task PlotTiles_SharedSlot_UndoRestoresSlot()
    {
        var (_, editor) = await OpenAsync();
        editor.EditMode = GraphicsEditMode.Tiles;
        editor.TilePalette!.SetBrush(TileBrush.Single(7, slot: 2));

        editor.BeginCellStroke("Plot Tiles");
        editor.PlotStroke(new Point(2, 0));
        editor.EndCellStroke();
        Assert.Equal(new MapEntry(7, 2, false, false, false), Grid(editor)[2, 0]);

        editor.EditMode = GraphicsEditMode.Arrange;
        editor.ApplyPaste(new ArrangerPaste(editor.WorkingArranger.CopyElements(2, 0, 1, 1), SnapMode.Element));
        Assert.Equal(new MapEntry(7, 2, false, false, false), Grid(editor)[0, 0]);

        editor.Undo();
        Assert.Equal(new MapEntry(1, 1, false, false, false), Grid(editor)[0, 0]);
        Assert.Equal(new MapEntry(7, 2, false, false, false), Grid(editor)[2, 0]);
        editor.Undo();
        Assert.Equal(new MapEntry(3, 0, false, false, false), Grid(editor)[2, 0]);
    }

    [Fact]
    public async Task Save_CellPastMapData_ShowsSaveErrorAndStaysModified()
    {
        using var shortMap = new MemoryDataSource("Short Map");
        shortMap.Write([.. Gba(1), .. Gba(2), .. Gba(3)]);
        using var tilemap = CreateTilemap(shortMap);
        var editor = new GraphicsEditorViewModel(tilemap, _interactions, CodecFixture.Shared.CodecService, _colorFactory,
            new PaletteStore(_globalPalette, [_globalPalette]), new ElementStore(), _projectService, _preferences,
            NullLogger<GraphicsEditorViewModel>.Instance) { EditMode = GraphicsEditMode.Tiles };
        editor.TilePalette!.SetBrush(TileBrush.Single(4));

        editor.BeginCellStroke("Plot Tiles");
        editor.PlotStroke(new Point(3, 0));
        editor.EndCellStroke();
        await editor.SaveChangesAsync();

        Assert.Equal(("Save Error", "Cell (3, 0) of tilemap 'Screen' has no entry in the map data"), _interactions.Alerts.Last());
        Assert.True(editor.IsModified);
        Assert.Equal([.. Gba(1), .. Gba(2), .. Gba(3)], shortMap.Read(BitAddress.Zero, 6 * 8));
    }
}
