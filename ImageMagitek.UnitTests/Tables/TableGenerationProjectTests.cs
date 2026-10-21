using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;
using ImageMagitek.Services;
using ImageMagitek.Tables;
using Xunit;

namespace ImageMagitek.UnitTests.Tables;

public sealed class TableGenerationProjectTests : IAsyncLifetime
{
    private readonly GenerationProjectContext _context = new(f => new ProjectService(f));
    private readonly List<ProjectTreeChange> _changes = [];

    public async Task InitializeAsync()
    {
        await _context.InitializeAsync();
        _context.Tree.Changed += (_, change) => _changes.Add(change);
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    private TableDefinition LoadDefinition()
    {
        var result = _context.Definitions.Load("Synthetic.table.xml");
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("\n", result.AsError.Reasons) : null);
        return result.AsSuccess.Result;
    }

    private GenerationPlan Plan(string folder = "/Generated")
    {
        var result = _context.Service.PlanGeneration(_context.Tree, "/Rom", LoadDefinition(), folder);
        Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
        return result.AsSuccess.Result;
    }

    [Fact]
    public void PlanGeneration_WritesNothing()
    {
        var before = _context.Snapshot();
        var directories = Directory.GetDirectories(_context.Directory, "*", SearchOption.AllDirectories);

        var plan = Plan();

        Assert.Equal(20, plan.Resources.Count);
        Assert.Equal("/Rom", plan.DataFileKey);
        Assert.Equal(_context.ManifestFile, plan.ManifestFileName);
        _context.AssertUnchanged(before);
        Assert.Equal(directories, Directory.GetDirectories(_context.Directory, "*", SearchOption.AllDirectories));
        Assert.False(_context.Tree.TryGetNode("/Generated", out _));
    }

    [Fact]
    public async Task Generate_SavesAndReloadsWithoutDefinition()
    {
        Assert.True((await _context.Service.GenerateResourcesAsync(_context.Tree, Plan())).HasSucceeded);
        _context.Definitions.Dispose();

        _context.Service.CloseProjects();
        var reopen = await _context.Service.OpenProjectFileAsync(_context.ProjectFile);
        Assert.True(reopen.HasSucceeded, reopen.HasFailed ? string.Join("\n", reopen.AsError.Reasons) : null);
        var tree = reopen.AsSuccess.Result;

        Assert.True(tree.TryGetItem<MetatileMap>("/Generated/Maps/Pallet Town/Pallet Town", out var map));
        Assert.True(tree.TryGetItem<MetatileSet>("/Generated/Tilesets/General", out var general));
        Assert.True(tree.TryGetItem<MetatileSet>("/Generated/Tilesets/Viridian City", out var viridian));
        Assert.Same(general, map.Sets[0].Set);
        Assert.Equal(4, viridian.Count);
        Assert.True(tree.TryGetItem<CompressedDataSource>("/Generated/Tilesets/Compressed/General Tiles", out _));
        Assert.True(tree.TryGetItem<Palette>("/Generated/Tilesets/Palettes/Pallet Town 12", out _));

        var files = Directory.GetFiles(_context.Directory, "*.xml", SearchOption.AllDirectories).ToDictionary(x => x, File.ReadAllBytes);
        Assert.True((await _context.Service.SaveProjectAsync(tree)).HasSucceeded);
        foreach (var (file, bytes) in files)
            Assert.Equal(bytes, File.ReadAllBytes(file));
    }

    [Fact]
    public async Task GenerateArrangers_SaveAndReloadWithoutDefinition()
    {
        File.WriteAllBytes(Path.Combine(_context.Directory, "ff4.bin"), Ff4ShapedData.Create());
        Assert.True((await _context.Service.AddResourceAsync(_context.Tree.Root, new FileDataSource("Ff4", Path.Combine(_context.Directory, "ff4.bin")))).HasSucceeded);
        var definition = _context.Definitions.Load("Ff4Synthetic.table.xml").AsSuccess.Result;
        var plan = _context.Service.PlanGeneration(_context.Tree, "/Ff4", definition, "/Monsters").AsSuccess.Result;

        var result = await _context.Service.GenerateResourcesAsync(_context.Tree, plan);
        Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
        var manifest = new XmlTableManifestReader(plan.ManifestSchemaFileName)
            .LoadFromFile(Path.Combine(_context.Definitions.Directory, "Ff4Synthetic.manifest.xml")).AsSuccess.Result;
        Assert.Contains(new TableManifestResource("/Monsters/02 Golbez", "arranger", "boss", "Monster", Ff4ShapedData.Monsters + 8), Assert.Single(manifest.Generations).Resources);
        _context.Definitions.Dispose();

        _context.Service.CloseProjects();
        var reopen = await _context.Service.OpenProjectFileAsync(_context.ProjectFile);
        Assert.True(reopen.HasSucceeded, reopen.HasFailed ? string.Join("\n", reopen.AsError.Reasons) : null);
        var tree = reopen.AsSuccess.Result;

        Assert.True(tree.TryGetItem<ScatteredArranger>("/Monsters/03 Zeromus", out var zeromus));
        Assert.True(tree.TryGetItem<Palette>("/Monsters/Monster Palettes/MonsterPal2x16", out var palette));
        var element = zeromus.GetElement(0, 0)!.Value;
        Assert.Equal(("SNES 4bpp", Ff4ShapedData.Boss4bppTiles + 64), (element.Codec.Name, element.SourceAddress.ByteOffset));
        Assert.Same(palette, ((IIndexedCodec)element.Codec).Palette);
        Assert.Null(zeromus.GetElement(1, 0));
    }

    [Fact]
    public async Task Generate_WritesResourcesAndManifestInOneTransaction()
    {
        var plan = Plan();

        var result = await _context.Service.GenerateResourcesAsync(_context.Tree, plan);

        Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
        foreach (var resource in plan.Resources)
        {
            Assert.True(_context.Tree.TryGetNode(resource.Key, out var node));
            Assert.True(File.Exists(node.DiskLocation));
            Assert.True(node.Model!.ResourceEquals(resource.Model));
        }

        Assert.Equal("1.1", XDocument.Load(_context.ProjectFile).Root!.Attribute("version")!.Value);
        Assert.Empty(Directory.EnumerateFiles(_context.Directory, "*.tmp", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(_context.Directory, "*.bak", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.Combine(_context.Directory, "_transaction.json")));

        var manifest = new XmlTableManifestReader(plan.ManifestSchemaFileName).LoadFromFile(_context.ManifestFile).AsSuccess.Result;
        var generation = Assert.Single(manifest.Generations);
        var expectedProject = Path.GetRelativePath(_context.Definitions.Directory, _context.ProjectFile).Replace('\\', '/');
        Assert.Equal((expectedProject, "/Generated", "/Rom", "Synthetic.table.xml"), (generation.Project, generation.Folder, generation.DataFile, generation.Definition));
        Assert.Equal(plan.Resources.Select(x => x.Key), generation.Resources.Select(x => x.Key));
        Assert.Contains(new TableManifestResource("/Generated/Maps/Pallet Town/Pallet Town", "metatilemap", "layoutMap", "Layout", FireRedShapedData.LayoutA), generation.Resources);

        var second = await _context.Service.GenerateResourcesAsync(_context.Tree, Plan("/Second"));
        Assert.True(second.HasSucceeded, second.HasFailed ? second.AsError.Reason : null);
        var merged = new XmlTableManifestReader(plan.ManifestSchemaFileName).LoadFromFile(_context.ManifestFile).AsSuccess.Result;
        Assert.Equal(["/Generated", "/Second"], merged.Generations.Select(x => x.Folder));
    }

    [Fact]
    public async Task Generate_KeyExists_FailsWritingNothing()
    {
        var folder = (await _context.Service.CreateNewFolderAsync(_context.Tree.Root, "Generated")).AsSuccess.Result;
        var tilesets = (await _context.Service.CreateNewFolderAsync(folder, "Tilesets")).AsSuccess.Result;
        var rom = (DataSource)_context.Tree.Root.ChildNodes.Single(x => x.Name == "Rom").Item;
        var palette = new Palette("general", new ColorFactory(), ColorModel.Bgr15, [new FileColorSource(0, Endian.Little)], false, PaletteStorageSource.ProjectXml, rom);
        Assert.True((await _context.Service.AddResourceAsync(tilesets, palette)).HasSucceeded);
        var plan = Plan();
        var before = _context.Snapshot();
        _changes.Clear();

        var result = await _context.Service.GenerateResourcesAsync(_context.Tree, plan);

        Assert.True(result.HasFailed);
        Assert.Contains("'/Generated/Tilesets/General' clashes with '/Generated/Tilesets/general'", result.AsError.Reason);
        _context.AssertUnchanged(before);
        Assert.Empty(_changes);
        Assert.False(File.Exists(_context.ManifestFile));
    }

    [Fact]
    public async Task FindGenerationConflicts_ListsClashWithoutWriting()
    {
        Assert.Empty(_context.Service.FindGenerationConflicts(_context.Tree, Plan()));

        var folder = (await _context.Service.CreateNewFolderAsync(_context.Tree.Root, "Generated")).AsSuccess.Result;
        var tilesets = (await _context.Service.CreateNewFolderAsync(folder, "Tilesets")).AsSuccess.Result;
        var rom = (DataSource)_context.Tree.Root.ChildNodes.Single(x => x.Name == "Rom").Item;
        var palette = new Palette("general", new ColorFactory(), ColorModel.Bgr15, [new FileColorSource(0, Endian.Little)], false, PaletteStorageSource.ProjectXml, rom);
        Assert.True((await _context.Service.AddResourceAsync(tilesets, palette)).HasSucceeded);
        var plan = Plan();
        var before = _context.Snapshot();
        _changes.Clear();

        var conflicts = _context.Service.FindGenerationConflicts(_context.Tree, plan);

        Assert.Contains("'/Generated/Tilesets/General' clashes with '/Generated/Tilesets/general'", conflicts);
        _context.AssertUnchanged(before);
        Assert.Empty(_changes);
    }

    [Fact]
    public async Task Generate_TransactionFails_RollsBackTreeAndDirectories()
    {
        File.WriteAllText(_context.ManifestFile, XmlTableManifestWriter.Serialize(TableManifest.Empty));
        File.SetAttributes(_context.ManifestFile, FileAttributes.ReadOnly);
        var plan = Plan();
        var before = _context.Snapshot();
        var directories = Directory.GetDirectories(_context.Directory, "*", SearchOption.AllDirectories);

        try
        {
            var result = await _context.Service.GenerateResourcesAsync(_context.Tree, plan);

            Assert.True(result.HasFailed);
            _context.AssertUnchanged(before);
            Assert.Equal(directories, Directory.GetDirectories(_context.Directory, "*", SearchOption.AllDirectories));
            Assert.False(_context.Tree.TryGetNode("/Generated", out _));
            Assert.Equal(["/Rom"], _context.Tree.Root.ChildNodes.Select(x => _context.Tree.CreatePathKey(x)));
            Assert.Contains(_changes, x => x.Kind == ProjectTreeChangeKind.Added);
            Assert.Equal(ProjectTreeChangeKind.Removed, _changes[^1].Kind);
        }
        finally
        {
            File.SetAttributes(_context.ManifestFile, FileAttributes.Normal);
        }

        Assert.True((await _context.Service.GenerateResourcesAsync(_context.Tree, Plan())).HasSucceeded);
    }

    [Fact]
    public async Task Generate_StandaloneTree_Fails()
    {
        var plan = Plan();
        File.WriteAllBytes(Path.Combine(_context.Directory, "standalone.bin"), FireRedShapedData.Create());
        var standalone = _context.Service.OpenDataFile(Path.Combine(_context.Directory, "standalone.bin")).AsSuccess.Result;

        var planResult = _context.Service.PlanGeneration(standalone, "/Rom", LoadDefinition(), "/Generated");
        var generateResult = await _context.Service.GenerateResourcesAsync(standalone, plan);

        Assert.Contains("standalone file", planResult.AsError.Reason);
        Assert.Contains("standalone file", generateResult.AsError.Reason);
        Assert.False(File.Exists(_context.ManifestFile));
    }
}
