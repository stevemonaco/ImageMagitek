using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Monaco.PathTree;
using Xunit;

namespace ImageMagitek.UnitTests.Tilemaps;

public sealed class TilemapProjectTests : IAsyncLifetime
{
    // Map: four GBA text BG entries at 0x10 for tiles 0-3, the last one with palette field 1
    private static readonly byte[] _map = [.. new byte[0x10], 0x00, 0x00, 0x01, 0x00, 0x02, 0x00, 0x03, 0x10];

    private readonly string _directory = TestPaths.CreateTempPath("");
    private readonly ColorFactory _colorFactory = new();
    private readonly Palette _globalPalette = ArrangerTestFactory.CreatePalette(new ColorRgba32(0, 0, 0, 255));
    private readonly MapFormatFactory _mapFormats = MapFormatTests.LoadShippedFormats();
    private readonly ProjectService _service;
    private ProjectTree _tree = null!;

    private string ProjectFile => Path.Combine(_directory, "project.xml");
    private string TilemapFile => Path.Combine(_directory, "Screen.xml");

    public TilemapProjectTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "map.bin"), _map);
        File.WriteAllBytes(Path.Combine(_directory, "tiles.bin"), new byte[4 * 32]);

        var serializerFactory = new XmlProjectSerializerFactory(Path.Combine(AppContext.BaseDirectory, "_schemas", "ResourceSchema.xsd"),
            CodecFixture.Shared.CodecFactory, _colorFactory, [_globalPalette], null, _mapFormats);
        _service = new ProjectService(serializerFactory);
    }

    public async Task InitializeAsync() =>
        _tree = (await _service.CreateNewProjectAsync(ProjectFile)).AsSuccess.Result;

    public Task DisposeAsync()
    {
        _service.CloseProjects();

        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }

        return Task.CompletedTask;
    }

    private async Task<(FileDataSource Map, FileDataSource Tiles, Palette Pal)> AddSourcesAsync()
    {
        var map = new FileDataSource("Map", Path.Combine(_directory, "map.bin"));
        var tiles = new FileDataSource("Tiles", Path.Combine(_directory, "tiles.bin"));
        Assert.True((await _service.AddResourceAsync(_tree.Root, map)).HasSucceeded);
        Assert.True((await _service.AddResourceAsync(_tree.Root, tiles)).HasSucceeded);

        var pal = new Palette("Pal", _colorFactory, ColorModel.Rgba32,
            Enumerable.Range(0, 16).Select(i => (IColorSource)new ProjectNativeColorSource(new ColorRgba32((byte)i, 0, 0, 255))).ToArray(),
            true, PaletteStorageSource.ProjectXml, map);
        Assert.True((await _service.AddResourceAsync(_tree.Root, pal)).HasSucceeded);

        return (map, tiles, pal);
    }

    private TilemapArranger CreateTilemap(DataSource map, DataSource tiles, Palette pal) =>
        new("Screen", map, 0x10, _mapFormats.Find("GBA Text BG")!, MapScreenLayout.Linear, 2, 2, tiles, 0, "GBA 4bpp", 0,
            [null, pal], CodecFixture.Shared.CodecFactory);

    private async Task<TilemapArranger> AddTilemapAsync()
    {
        var (map, tiles, pal) = await AddSourcesAsync();
        var tilemap = CreateTilemap(map, tiles, pal);
        Assert.True((await _service.AddResourceAsync(_tree.Root, tilemap)).HasSucceeded);
        return tilemap;
    }

    private async Task<ProjectTree> ReopenAsync()
    {
        _service.CloseProjects();
        var result = await _service.OpenProjectFileAsync(ProjectFile);
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("\n", result.AsError.Reasons) : null);
        return _tree = result.AsSuccess.Result;
    }

    private static string ReadVersion(string projectFile) =>
        System.Xml.Linq.XDocument.Load(projectFile).Root!.Attribute("version")!.Value;

    [Fact]
    public async Task SaveAndReload_RoundTripsByteIdentically_AndWritesVersion11()
    {
        await AddTilemapAsync();

        var tilemapXml = File.ReadAllText(TilemapFile);
        Assert.Contains("""<tilemap mapdatafile="/Map" mapoffset="10" width="2" height="2" format="GBA Text BG" tiledatafile="/Tiles" tileoffset="0" codec="GBA 4bpp">""", tilemapXml);
        Assert.Contains("""<paletteslot index="1" palette="/Pal" />""", tilemapXml);
        Assert.Equal("1.1", ReadVersion(ProjectFile));

        var files = Directory.GetFiles(_directory, "*.xml").ToDictionary(x => x, File.ReadAllBytes);
        var reopened = await ReopenAsync();
        Assert.True((await _service.SaveProjectAsync(reopened)).HasSucceeded);

        foreach (var (fileName, bytes) in files)
            Assert.Equal(bytes, File.ReadAllBytes(fileName));

        Assert.True(reopened.TryGetItem<TilemapArranger>("/Screen", out var loaded));
        Assert.Equal(0x10, loaded.MapOffset);
        Assert.Equal("GBA Text BG", loaded.MapFormat.Name);
        Assert.Null(loaded.PaletteSlots[0]);
        Assert.True(reopened.TryGetItem<Palette>("/Pal", out var pal));
        Assert.Same(pal, loaded.PaletteSlots[1]);
        Assert.Same(pal, ((Codec.IIndexedCodec)loaded.GetElement(1, 1)!.Value.Codec).Palette);
        Assert.Equal(3 * 32, loaded.GetElement(1, 1)!.Value.SourceAddress.ByteOffset);
    }

    [Fact]
    public async Task ProjectWithoutTilemapOrCompressedData_WritesVersion10()
    {
        await AddSourcesAsync();
        Assert.True((await _service.SaveProjectAsync(_tree)).HasSucceeded);

        Assert.Equal("1.0", ReadVersion(ProjectFile));
    }

    [Fact]
    public async Task Version09Project_SavedWithoutChanges_IsNotRewritten()
    {
        _service.CloseProjects();
        File.WriteAllText(ProjectFile, """<?xml version="1.0" encoding="utf-8"?><project version="0.9" />""");
        var bytes = File.ReadAllBytes(ProjectFile);

        var reopened = await ReopenAsync();
        Assert.True((await _service.SaveProjectAsync(reopened)).HasSucceeded);

        Assert.Equal(bytes, File.ReadAllBytes(ProjectFile));
    }

    [Fact]
    public async Task AddingTilemap_RaisesVersion_DeletingLastReturnsTo10OnSave()
    {
        var (map, tiles, pal) = await AddSourcesAsync();
        Assert.Equal("1.0", ReadVersion(ProjectFile));

        Assert.True((await _service.AddResourceAsync(_tree.Root, CreateTilemap(map, tiles, pal))).HasSucceeded);
        Assert.Equal("1.1", ReadVersion(ProjectFile));

        Assert.True(_tree.TryGetNode("/Screen", out var node));
        var plan = _service.PreviewResourceDeletion(node).AsSuccess.Result;
        Assert.True((await _service.ApplyResourceDeletionAsync(plan, _globalPalette)).HasSucceeded);
        Assert.True((await _service.SaveProjectAsync(_tree)).HasSucceeded);

        Assert.Equal("1.0", ReadVersion(ProjectFile));
    }

    [Theory]
    [InlineData("mapdatafile=\"/Map\"", "mapdatafile=\"/Gone\"", "/Gone")]
    [InlineData("tiledatafile=\"/Tiles\"", "tiledatafile=\"/Gone\"", "/Gone")]
    [InlineData("format=\"GBA Text BG\"", "format=\"No Such Format\"", "No Such Format")]
    [InlineData("codec=\"GBA 4bpp\"", "codec=\"No Such Codec\"", "No Such Codec")]
    [InlineData("palette=\"/Pal\"", "palette=\"/Gone\"", "/Gone")]
    public async Task Load_UnresolvedReference_FailsNamingIt(string original, string replacement, string expectedName)
    {
        await AddTilemapAsync();
        _service.CloseProjects();
        File.WriteAllText(TilemapFile, File.ReadAllText(TilemapFile).Replace(original, replacement));

        var result = await _service.OpenProjectFileAsync(ProjectFile);

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains("/Screen") && x.Contains(expectedName));
    }

    [Fact]
    public async Task Load_DirectCodec_FailsNamingIt()
    {
        await AddTilemapAsync();
        _service.CloseProjects();
        var directCodec = CodecFixture.Shared.DirectCodecNames[0];
        File.WriteAllText(TilemapFile, File.ReadAllText(TilemapFile).Replace("codec=\"GBA 4bpp\"", $"codec=\"{directCodec}\""));

        var result = await _service.OpenProjectFileAsync(ProjectFile);

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains("direct codec") && x.Contains(directCodec));
    }

    [Theory]
    [InlineData("/Map")]
    [InlineData("/Tiles")]
    [InlineData("/Pal")]
    public async Task DeletingReferencedResource_RemovesTilemap(string deletedKey)
    {
        await AddTilemapAsync();
        Assert.True(_tree.TryGetNode(deletedKey, out var deleted));

        var plan = _service.PreviewResourceDeletion(deleted).AsSuccess.Result;

        var change = Assert.Single(plan.Changes, x => x.Resource is TilemapArranger);
        Assert.True(change.Removed);

        Assert.True((await _service.ApplyResourceDeletionAsync(plan, _globalPalette)).HasSucceeded);
        Assert.False(_tree.TryGetNode("/Screen", out _));
        Assert.False(File.Exists(TilemapFile));
    }

    [Fact]
    public async Task RenamingSlotPalette_RewritesTilemap()
    {
        await AddTilemapAsync();
        Assert.True(_tree.TryGetNode("/Pal", out var palNode));

        Assert.True((await _service.RenameResourceAsync(palNode, "Renamed")).HasSucceeded);

        Assert.Contains("""<paletteslot index="1" palette="/Renamed" />""", File.ReadAllText(TilemapFile));
    }
}
