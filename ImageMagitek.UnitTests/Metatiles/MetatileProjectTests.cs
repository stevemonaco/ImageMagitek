using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.Tilemaps;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using ImageMagitek.UnitTests.Tilemaps;
using Xunit;

namespace ImageMagitek.UnitTests.Metatiles;

public sealed class MetatileProjectTests : IAsyncLifetime
{
    private readonly string _directory = TestPaths.CreateTempPath("");
    private readonly ColorFactory _colorFactory = new();
    private readonly Palette _globalPalette = ArrangerTestFactory.CreatePalette(new ColorRgba32(0, 0, 0, 255));
    private readonly MapFormatFactory _mapFormats = MapFormatTests.LoadShippedFormats();
    private readonly ProjectService _service;
    private ProjectTree _tree = null!;

    private string ProjectFile => Path.Combine(_directory, "project.xml");
    private string PrimaryFile => Path.Combine(_directory, "Primary.xml");
    private string MapFile => Path.Combine(_directory, "Town.xml");

    public MetatileProjectTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "rom.bin"), new byte[0x4000]);

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

    private async Task<T> AddAsync<T>(T resource) where T : IProjectResource
    {
        var result = await _service.AddResourceAsync(_tree.Root, resource);
        Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
        return resource;
    }

    private Palette CreatePalette(string name, DataSource source) =>
        new(name, _colorFactory, ColorModel.Rgba32,
            Enumerable.Range(0, 16).Select(i => (IColorSource)new ProjectNativeColorSource(new ColorRgba32((byte)i, 0, 0, 255))).ToArray(),
            true, PaletteStorageSource.ProjectXml, source);

    /// <summary>
    /// A primary set (base 0, 640 tiles, attributes) and a planar secondary set (base 640) over one data file, and a two-set map
    /// </summary>
    private async Task<(FileDataSource Rom, MetatileSet Primary, MetatileSet Secondary, MetatileMap Map)> AddTwoSetMapAsync()
    {
        var rom = await AddAsync(new FileDataSource("Rom", Path.Combine(_directory, "rom.bin")));
        var low = await AddAsync(CreatePalette("Low", rom));
        var high = await AddAsync(CreatePalette("High", rom));
        var gba = _mapFormats.Find("GBA Text BG")!;
        var codecs = CodecFixture.Shared.CodecFactory;

        var primary = await AddAsync(new MetatileSet("Primary", rom, 0x100, 640, new Size(2, 2), 2, gba, MetatileOrder.Interleaved,
            rom, 0, "GBA 4bpp", 0, 640, [low], codecs, rom, 0x80, 4));
        var secondary = await AddAsync(new MetatileSet("Secondary", rom, 0x2A0, 4, new Size(2, 2), 2, gba, MetatileOrder.Planar,
            rom, 0x3000, "GBA 4bpp", 640, 8, [null, high], codecs));
        var map = await AddAsync(new MetatileMap("Town", rom, 0x10, _mapFormats.Find("FRLG Block")!, MapScreenLayout.Linear, 3, 2,
            [new MetatileMapSet(primary, 0), new MetatileMapSet(secondary, 640)], emptyBlock: 1));

        return (rom, primary, secondary, map);
    }

    private async Task<ProjectTree> ReopenAsync()
    {
        _service.CloseProjects();
        var result = await _service.OpenProjectFileAsync(ProjectFile);
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("\n", result.AsError.Reasons) : null);
        return _tree = result.AsSuccess.Result;
    }

    [Fact]
    public async Task SaveAndReload_TwoSetMap_RoundTripsByteIdentically_AndWritesVersion11()
    {
        await AddTwoSetMapAsync();

        Assert.Contains("""<metatileset metatiledatafile="/Rom" metatileoffset="100" metatilecount="640" layers="2" format="GBA Text BG" tiledatafile="/Rom" tileoffset="0" codec="GBA 4bpp" tilecount="640" attributedatafile="/Rom" attributeoffset="80" attributesize="4">""",
            File.ReadAllText(PrimaryFile));
        Assert.Contains("""<paletteslot index="0" palette="/Low" />""", File.ReadAllText(PrimaryFile));

        var secondaryXml = File.ReadAllText(Path.Combine(_directory, "Secondary.xml"));
        Assert.Contains("""order="planar" """, secondaryXml);
        Assert.Contains("""firsttile="640" """, secondaryXml);
        Assert.Contains("""<paletteslot index="1" palette="/High" />""", secondaryXml);

        var mapXml = File.ReadAllText(MapFile);
        Assert.Contains("""<metatilemap mapdatafile="/Rom" mapoffset="10" width="3" height="2" format="FRLG Block" emptyblock="1">""", mapXml);
        Assert.True(mapXml.IndexOf("""<set metatileset="/Primary" base="0" />""") < mapXml.IndexOf("""<set metatileset="/Secondary" base="640" />"""));
        Assert.Equal("1.1", System.Xml.Linq.XDocument.Load(ProjectFile).Root!.Attribute("version")!.Value);

        var files = Directory.GetFiles(_directory, "*.xml").ToDictionary(x => x, File.ReadAllBytes);
        var reopened = await ReopenAsync();
        Assert.True((await _service.SaveProjectAsync(reopened)).HasSucceeded);

        foreach (var (fileName, bytes) in files)
            Assert.Equal(bytes, File.ReadAllBytes(fileName));

        Assert.True(reopened.TryGetItem<MetatileMap>("/Town", out var map));
        Assert.True(reopened.TryGetItem<MetatileSet>("/Primary", out var primary));
        Assert.True(reopened.TryGetItem<MetatileSet>("/Secondary", out var secondary));
        Assert.Equal(new[] { (primary, 0), (secondary, 640) }, map.Sets.Select(x => (x.Set, x.Base)));
        Assert.Equal(MetatileOrder.Planar, secondary.Order);
        Assert.Equal(4, primary.AttributeSize);
        Assert.Equal(1, map.EmptyBlock);
        Assert.True(reopened.TryGetItem<Palette>("/High", out var high));
        Assert.Same(high, map.PaletteSlots[1]);
    }

    [Fact]
    public async Task Load_MissingSet_FailsNamingIt()
    {
        await AddTwoSetMapAsync();
        _service.CloseProjects();
        File.WriteAllText(MapFile, File.ReadAllText(MapFile).Replace("metatileset=\"/Secondary\"", "metatileset=\"/Gone\""));

        var result = await _service.OpenProjectFileAsync(ProjectFile);

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains("/Town") && x.Contains("metatile set '/Gone'"));
    }

    [Theory]
    [InlineData("metatiledatafile=\"/Rom\"", "metatiledatafile=\"/Gone\"", "/Gone")]
    [InlineData("format=\"GBA Text BG\"", "format=\"No Such Format\"", "No Such Format")]
    [InlineData("codec=\"GBA 4bpp\"", "codec=\"No Such Codec\"", "No Such Codec")]
    [InlineData("palette=\"/Low\"", "palette=\"/Gone\"", "/Gone")]
    public async Task Load_UnresolvedReference_FailsNamingIt(string original, string replacement, string expectedName)
    {
        await AddTwoSetMapAsync();
        _service.CloseProjects();
        File.WriteAllText(PrimaryFile, File.ReadAllText(PrimaryFile).Replace(original, replacement));

        var result = await _service.OpenProjectFileAsync(ProjectFile);

        Assert.True(result.HasFailed);
        Assert.Contains(result.AsError.Reasons, x => x.Contains("/Primary") && x.Contains(expectedName));
    }

    [Fact]
    public async Task DeletingSet_RemovesMapsThatListIt()
    {
        var (_, _, secondary, map) = await AddTwoSetMapAsync();
        Assert.True(_tree.TryGetNode("/Secondary", out var setNode));

        var plan = _service.PreviewResourceDeletion(setNode).AsSuccess.Result;

        Assert.True(Assert.Single(plan.Changes, x => ReferenceEquals(x.Resource, map)).Removed);
        Assert.DoesNotContain(plan.Changes, x => x.Resource is MetatileSet set && set != secondary);

        Assert.True((await _service.ApplyResourceDeletionAsync(plan, _globalPalette)).HasSucceeded);
        Assert.False(_tree.TryGetNode("/Town", out _));
        Assert.True(_tree.TryGetNode("/Primary", out _));
        Assert.False(File.Exists(MapFile));
    }

    [Theory]
    [InlineData("/Rom")]
    public async Task DeletingDataSource_RemovesSetsAndMaps(string deletedKey)
    {
        var (_, primary, _, map) = await AddTwoSetMapAsync();
        Assert.True(_tree.TryGetNode(deletedKey, out var deleted));

        var plan = _service.PreviewResourceDeletion(deleted).AsSuccess.Result;

        Assert.True(Assert.Single(plan.Changes, x => ReferenceEquals(x.Resource, primary)).Removed);
        Assert.True(Assert.Single(plan.Changes, x => ReferenceEquals(x.Resource, map)).Removed);

        Assert.True((await _service.ApplyResourceDeletionAsync(plan, _globalPalette)).HasSucceeded);
        Assert.False(_tree.TryGetNode("/Primary", out _));
        Assert.False(_tree.TryGetNode("/Town", out _));
        Assert.False(File.Exists(PrimaryFile));
        Assert.False(File.Exists(MapFile));
    }

    [Fact]
    public async Task DeletingSlotPalette_KeepsSetAndMaps_AndClearsSlot()
    {
        var (_, primary, secondary, map) = await AddTwoSetMapAsync();
        Assert.True(_tree.TryGetNode("/Low", out var lowNode));

        var plan = _service.PreviewResourceDeletion(lowNode).AsSuccess.Result;

        var setChange = Assert.Single(plan.Changes, x => ReferenceEquals(x.Resource, primary));
        var mapChange = Assert.Single(plan.Changes, x => ReferenceEquals(x.Resource, map));
        Assert.False(setChange.Removed);
        Assert.True(setChange.LostSlotPalette);
        Assert.False(mapChange.Removed);
        Assert.True(mapChange.LostSlotPalette);
        Assert.DoesNotContain(plan.Changes, x => ReferenceEquals(x.Resource, secondary));

        Assert.True((await _service.ApplyResourceDeletionAsync(plan, _globalPalette)).HasSucceeded);
        Assert.DoesNotContain("/Low", File.ReadAllText(PrimaryFile));
        Assert.True(File.Exists(MapFile));
        Assert.True(_tree.TryGetItem<MetatileSet>("/Primary", out var rebuiltPrimary));
        Assert.True(_tree.TryGetItem<MetatileMap>("/Town", out var rebuiltMap));
        Assert.NotSame(primary, rebuiltPrimary);
        Assert.NotSame(map, rebuiltMap);
        Assert.Same(rebuiltPrimary, rebuiltMap.Sets[0].Set);
        Assert.Same(secondary, rebuiltMap.Sets[1].Set);
        Assert.Null(rebuiltPrimary.PaletteSlots[0]);

        var reopened = await ReopenAsync();
        Assert.True(reopened.TryGetItem<MetatileMap>("/Town", out var loaded));
        Assert.Null(loaded.Sets[0].Set.PaletteSlots[0]);
    }

    [Fact]
    public async Task RenamingSet_RewritesMapSetKey()
    {
        await AddTwoSetMapAsync();
        Assert.True(_tree.TryGetNode("/Secondary", out var setNode));

        Assert.True((await _service.RenameResourceAsync(setNode, "Pallet")).HasSucceeded);

        Assert.Contains("""<set metatileset="/Pallet" base="640" />""", File.ReadAllText(MapFile));
        await ReopenAsync();
    }
}
