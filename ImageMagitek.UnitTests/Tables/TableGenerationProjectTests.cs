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
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.Tables;
using ImageMagitek.UnitTests.Fixtures;
using Monaco.PathTree;
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

    private GenerationPlan Plan(string folder = "/Generated", bool overwriteEdited = false)
    {
        var result = _context.Service.PlanGeneration(_context.Tree, "/Rom", LoadDefinition(), folder, overwriteEdited);
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
    public void PlanGeneration_UnreadableManifest_Fails()
    {
        File.WriteAllText(_context.ManifestFile, "<tablemanifest version=\"2.0\" />");

        var result = _context.Service.PlanGeneration(_context.Tree, "/Rom", LoadDefinition(), "/Generated");

        Assert.True(result.HasFailed);
        Assert.Contains($"Manifest '{_context.ManifestFile}' could not be read", result.AsError.Reason);
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
        Assert.Contains(new TableManifestResource("/Monsters/02 Golbez", "arranger", "boss", "Monster", Ff4ShapedData.Monsters + 8),
            Assert.Single(manifest.Generations).Resources.Select(x => x with { Path = null, Hash = null }));
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
            Assert.Equal(resource.Model, node.Model);
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
        Assert.Contains(new TableManifestResource("/Generated/Maps/Pallet Town/Pallet Town", "metatilemap", "layoutMap", "Layout", FireRedShapedData.LayoutA),
            generation.Resources.Select(x => x with { Path = null, Hash = null }));
        Assert.Equal(plan.Resources.Select(x => x.Path), generation.Resources.Select(x => x.Path));
        Assert.All(generation.Resources, x => Assert.Equal(TableManifest.HashFile(Node(x.Key).DiskLocation!), x.Hash));

        var second = await _context.Service.GenerateResourcesAsync(_context.Tree, Plan("/Second"));
        Assert.True(second.HasSucceeded, second.HasFailed ? second.AsError.Reason : null);
        var merged = new XmlTableManifestReader(plan.ManifestSchemaFileName).LoadFromFile(_context.ManifestFile).AsSuccess.Result;
        Assert.Equal(["/Generated", "/Second"], merged.Generations.Select(x => x.Folder));
    }

    private async Task<ResourceNode> AddUnsavedPalette()
    {
        var rom = (DataSource)_context.Tree.Root.ChildNodes.Single(x => x.Name == "Rom").Item;
        var palette = new Palette("Other", new ColorFactory(), ColorModel.Bgr15, [new FileColorSource(0, Endian.Little)], false, PaletteStorageSource.ProjectXml, rom);
        Assert.True((await _context.Service.AddResourceAsync(_context.Tree.Root, palette)).HasSucceeded);
        Assert.True(_context.Tree.TryGetNode("/Other", out var node));
        File.WriteAllText(node.DiskLocation!, "unsaved");
        node.Model = null;
        return node;
    }

    [Fact]
    public async Task Generate_LeavesOtherChangedResourcesUnwritten()
    {
        var other = await AddUnsavedPalette();

        var result = await _context.Service.GenerateResourcesAsync(_context.Tree, Plan());

        Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
        Assert.Equal("unsaved", File.ReadAllText(other.DiskLocation!));
        Assert.Null(other.Model);
    }

    [Fact]
    public async Task Generate_SaveProject_WritesOtherChangedResources()
    {
        var other = await AddUnsavedPalette();

        var result = await _context.Service.GenerateResourcesAsync(_context.Tree, Plan(), saveProject: true);

        Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
        Assert.NotEqual("unsaved", File.ReadAllText(other.DiskLocation!));
        Assert.NotNull(other.Model);
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

    private const string MapA = "/Generated/Maps/Pallet Town/Pallet Town";
    private const string Border = "/Generated/Borders/Pallet Town/Pallet Town Border";
    private const string Palette12 = "/Generated/Tilesets/Palettes/Pallet Town 12";
    private const string PalletSet = "/Generated/Tilesets/Pallet Town";

    private async Task GenerateAsync(GenerationPlan? plan = null, bool confirmRemovals = false)
    {
        var result = await _context.Service.GenerateResourcesAsync(_context.Tree, plan ?? Plan(), confirmRemovals: confirmRemovals);
        Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
    }

    private TableManifestGeneration ReadGeneration() => ReadGeneration(_context.ManifestFile, "/Generated");

    private static TableManifestGeneration ReadGeneration(string manifestFile, string folder) =>
        new XmlTableManifestReader(Path.Combine(TableTestContext.SchemaDirectory, XmlTableDefinitionReader.ManifestSchemaFileName))
            .LoadFromFile(manifestFile).AsSuccess.Result.Generations.Single(x => x.Folder == folder);

    private ResourceNode Node(string key)
    {
        Assert.True(_context.Tree.TryGetNode(key, out var node), $"'{key}' is not in the tree");
        return node;
    }

    private static void RepointMapA(byte[] rom) => FireRedShapedData.RepointLayoutBlocks(rom, FireRedShapedData.LayoutA, FireRedShapedData.FreeSpace);

    private void DropLastSecondaryPalette() =>
        _context.EditDefinition(x => x.Replace("repeat=\"isSecondary ? 6 : 7\"", "repeat=\"isSecondary ? 5 : 7\""));

    private static void MarkEdited(string file) =>
        File.WriteAllText(file, File.ReadAllText(file).Replace("<metatilemap", "<!-- edited -->\n<metatilemap"));

    private async Task MoveAsync(string key, string folderKey) =>
        Assert.True((await _context.Service.MoveNodeAsync(Node(key), Node(folderKey))).HasSucceeded);

    [Fact]
    public async Task Regenerate_FindsMovedResourceByHash()
    {
        await GenerateAsync();
        await MoveAsync(MapA, "/Generated");

        var regeneration = Plan().Regeneration!;

        var map = Assert.Single(regeneration.Resources, x => x.Planned.Key == MapA);
        Assert.Equal(("/Generated/Pallet Town", false), (map.Entry!.CurrentKey, map.Entry.IsEdited));
        Assert.Equal((RegenerationKind.Unchanged, "/Generated/Pallet Town"), (map.Kind, map.TargetKey));
        Assert.All(regeneration.Resources, x => Assert.Equal(RegenerationKind.Unchanged, x.Kind));
        Assert.Empty(regeneration.Removed);
        Assert.Empty(regeneration.Kept);
    }

    [Fact]
    public async Task Regenerate_DataFileDiffers_Fails()
    {
        await GenerateAsync();
        File.Copy(_context.RomFile, Path.Combine(_context.Directory, "rom2.bin"));
        Assert.True((await _context.Service.AddResourceAsync(_context.Tree.Root, new FileDataSource("Rom2", Path.Combine(_context.Directory, "rom2.bin")))).HasSucceeded);

        var result = _context.Service.PlanGeneration(_context.Tree, "/Rom2", LoadDefinition(), "/Generated");

        Assert.True(result.HasFailed);
        Assert.Contains("'/Rom'", result.AsError.Reason);
        Assert.Contains("'/Rom2'", result.AsError.Reason);
    }

    [Fact]
    public async Task Regenerate_MovedResourceKeepsKey()
    {
        await GenerateAsync();
        await MoveAsync(MapA, "/Generated");
        await _context.ReopenAsync(RepointMapA);

        var plan = Plan();
        var map = Assert.Single(plan.Regeneration!.Resources, x => x.Planned.Key == MapA);
        Assert.Equal((RegenerationKind.Changed, "/Generated/Pallet Town"), (map.Kind, map.TargetKey));
        Assert.Equal(["repointed"], map.ChangeKinds);

        await GenerateAsync(plan);

        Assert.Equal(FireRedShapedData.FreeSpace, Assert.IsType<MetatileMapModel>(Node("/Generated/Pallet Town").Model).MapOffset);
        Assert.False(_context.Tree.TryGetNode(MapA, out _));
        Assert.Contains(ReadGeneration().Resources, x => x.Key == "/Generated/Pallet Town");
    }

    [Fact]
    public async Task Regenerate_RenamedTemplateRenamesUnmovedResource()
    {
        await GenerateAsync();
        var oldFile = Node(Border).DiskLocation!;
        _context.EditDefinition(x => x.Replace("{name:layoutMap} Border", "{name:layoutMap} Edge"));
        const string renamed = "/Generated/Borders/Pallet Town/Pallet Town Edge";

        var plan = Plan();
        var border = Assert.Single(plan.Regeneration!.Resources, x => x.Entry?.Entry.Key == Border);
        Assert.Equal((RegenerationKind.Changed, renamed), (border.Kind, border.TargetKey));
        Assert.Empty(_context.Service.FindGenerationConflicts(_context.Tree, plan));

        await GenerateAsync(plan);

        Assert.True(File.Exists(Node(renamed).DiskLocation));
        Assert.False(_context.Tree.TryGetNode(Border, out _));
        Assert.False(File.Exists(oldFile));
        Assert.Contains(ReadGeneration().Resources, x => x.Key == renamed);
    }

    [Fact]
    public async Task Regenerate_EditedResourceKept()
    {
        await GenerateAsync();
        var mapFile = Node(MapA).DiskLocation!;
        var entry = ReadGeneration().Resources.Single(x => x.Key == MapA);
        MarkEdited(mapFile);
        await _context.ReopenAsync(RepointMapA);

        var plan = Plan();
        Assert.Contains($"Kept edited {MapA}", plan.Regeneration!.SummaryLines(false));
        Assert.DoesNotContain(plan.Regeneration.Resources, x => x.Planned.Key == MapA);
        var bytes = File.ReadAllBytes(mapFile);

        await GenerateAsync(plan);

        Assert.Equal(bytes, File.ReadAllBytes(mapFile));
        Assert.Equal(entry, ReadGeneration().Resources.Single(x => x.Key == MapA));
    }

    [Fact]
    public async Task Regenerate_OverwriteEdited_Writes()
    {
        await GenerateAsync();
        var mapFile = Node(MapA).DiskLocation!;
        MarkEdited(mapFile);
        await _context.ReopenAsync(RepointMapA);

        var plan = Plan(overwriteEdited: true);
        var map = Assert.Single(plan.Regeneration!.Resources, x => x.Planned.Key == MapA);
        Assert.True(map.Entry!.IsEdited);
        Assert.Equal(RegenerationKind.Changed, map.Kind);

        await GenerateAsync(plan);

        Assert.DoesNotContain("edited", File.ReadAllText(mapFile));
        Assert.Equal(FireRedShapedData.FreeSpace, Assert.IsType<MetatileMapModel>(Node(MapA).Model).MapOffset);
        Assert.Equal(TableManifest.HashFile(mapFile), ReadGeneration().Resources.Single(x => x.Key == MapA).Hash);
    }

    [Fact]
    public async Task Regenerate_DeletedResourceNotRecreated()
    {
        await GenerateAsync();
        var deletion = _context.Service.PreviewResourceDeletion(Node(Border)).AsSuccess.Result;
        Assert.True((await _context.Service.ApplyResourceDeletionAsync(deletion, ProjectFixtures.DefaultPalette)).HasSucceeded);

        var plan = Plan();
        Assert.Contains($"Kept missing {Border}", plan.Regeneration!.SummaryLines(false));
        Assert.Empty(plan.Regeneration.Written);

        await GenerateAsync(plan);

        Assert.False(_context.Tree.TryGetNode(Border, out _));
        Assert.Contains(ReadGeneration().Resources, x => x.Key == Border);
        Assert.Empty(Plan().Regeneration!.Written);
    }

    [Fact]
    public async Task Regenerate_RemovedPaletteUsedByUserArranger_Kept()
    {
        await GenerateAsync();
        var palette = (Palette)Node(Palette12).Item;
        var codec = (IIndexedCodec)CodecFixture.Shared.CodecFactory.CreateCodec("GBA 4bpp")!;
        codec.Palette = palette;
        var arranger = new ScatteredArranger("User", PixelColorType.Indexed, ElementLayout.Tiled, 1, 1, codec.Width, codec.Height);
        arranger.SetElement(new ArrangerElement(0, 0, (DataSource)Node("/Rom").Item, BitAddress.Zero, codec), 0, 0);
        Assert.True((await _context.Service.AddResourceAsync(_context.Tree.Root, arranger)).HasSucceeded);
        DropLastSecondaryPalette();

        var plan = Plan();
        Assert.Empty(plan.Regeneration!.Removed);
        Assert.Contains($"Kept referenced {Palette12}: used by /User", plan.Regeneration.SummaryLines(false));
        Assert.Contains("re-paletted", Assert.Single(plan.Regeneration.Resources, x => x.TargetKey == PalletSet).ChangeKinds);

        await GenerateAsync(plan);

        Assert.Same(palette, Node(Palette12).Item);
        Assert.Same(palette, ((IIndexedCodec)arranger.GetElement(0, 0)!.Value.Codec).Palette);
        Assert.Contains(ReadGeneration().Resources, x => x.Key == Palette12);
        var set = (MetatileSet)Node(PalletSet).Item;
        Assert.Same(set, ((MetatileMap)Node(MapA).Item).Sets[1].Set);
    }

    [Fact]
    public async Task Regenerate_RemovedRecord_DeletesResource()
    {
        await GenerateAsync();
        var paletteFile = Node(Palette12).DiskLocation!;
        DropLastSecondaryPalette();

        var plan = Plan();
        Assert.Equal(Palette12, Assert.Single(plan.Regeneration!.Removed).CurrentKey);
        Assert.Contains($"Removed {Palette12}", plan.Regeneration.SummaryLines(false));

        await GenerateAsync(plan, confirmRemovals: true);

        Assert.False(_context.Tree.TryGetNode(Palette12, out _));
        Assert.False(File.Exists(paletteFile));
        Assert.Equal(Enumerable.Range(7, 5), Assert.IsType<MetatileSetModel>(Node(PalletSet).Model).PaletteSlots.Keys.Order());
        Assert.Same(Node(PalletSet).Item, ((MetatileMap)Node(MapA).Item).Sets[1].Set);

        await _context.ReopenAsync();
        Assert.Equal(5, ((MetatileSet)Node(PalletSet).Item).PaletteSlots.OfType<Palette>().Count());
    }

    [Fact]
    public async Task Regenerate_RemovalsUnconfirmed_FailsWritingNothing()
    {
        await GenerateAsync();
        DropLastSecondaryPalette();
        var plan = Plan();
        var before = _context.Snapshot();
        _changes.Clear();

        var result = await _context.Service.GenerateResourcesAsync(_context.Tree, plan);

        Assert.True(result.HasFailed);
        Assert.Contains("would remove 1 resources", result.AsError.Reason);
        Assert.Contains(Palette12, result.AsError.Reason);
        _context.AssertUnchanged(before);
        Assert.Empty(_changes);
        Assert.True(_context.Tree.TryGetNode(Palette12, out _));
    }

    [Fact]
    public async Task Regenerate_WritesOnlyChangedResources()
    {
        await GenerateAsync();
        var rom = (DataSource)Node("/Rom").Item;
        var mine = new Palette("Mine", new ColorFactory(), ColorModel.Bgr15, [new FileColorSource(0, Endian.Little)], false, PaletteStorageSource.ProjectXml, rom);
        Assert.True((await _context.Service.AddResourceAsync(Node("/Generated"), mine)).HasSucceeded);
        await _context.ReopenAsync(RepointMapA);
        var mapFile = Node(MapA).DiskLocation!;
        var before = _context.Snapshot();

        var plan = Plan();
        Assert.Equal([
            "Unchanged 1 compresseddata",
            "Unchanged 13 palette",
            "Unchanged 3 metatileset",
            "Changed 1 metatilemap",
            "Unchanged 2 metatilemap",
            $"Changed {MapA}: repointed"], plan.Regeneration!.SummaryLines(false));

        await GenerateAsync(plan);

        var after = _context.Snapshot();
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        Assert.Equal(new[] { mapFile, _context.ManifestFile }.Order(), before.Keys.Where(x => !before[x].SequenceEqual(after[x])).Order());
        Assert.True(_context.Tree.TryGetItem<Palette>("/Generated/Mine", out _));
        Assert.Equal(TableManifest.HashFile(mapFile), ReadGeneration().Resources.Single(x => x.Key == MapA).Hash);
        Assert.Empty(Plan().Regeneration!.Written);
    }

    [Fact]
    public async Task Regenerate_TransactionFails_RollsBack()
    {
        await GenerateAsync();
        await _context.ReopenAsync(RepointMapA);
        var plan = Plan();
        var before = _context.Snapshot();
        var nodes = _context.Tree.EnumerateDepthFirst().ToList();
        var mapNode = Node(MapA);
        File.SetAttributes(_context.ManifestFile, FileAttributes.ReadOnly);

        try
        {
            var result = await _context.Service.GenerateResourcesAsync(_context.Tree, plan);

            Assert.True(result.HasFailed);
            _context.AssertUnchanged(before);
            Assert.Equal(nodes, _context.Tree.EnumerateDepthFirst().ToList());
            Assert.Same(mapNode, Node(MapA));
            Assert.Same(mapNode.Item, ((MetatileMapNode)Node(MapA)).Item);
        }
        finally
        {
            File.SetAttributes(_context.ManifestFile, FileAttributes.Normal);
        }

        await GenerateAsync(Plan());
        Assert.Equal(FireRedShapedData.FreeSpace, Assert.IsType<MetatileMapModel>(Node(MapA).Model).MapOffset);
    }

    [Fact]
    public async Task Regenerate_ManifestListsKeptAndDropsRemoved()
    {
        await GenerateAsync();
        var first = ReadGeneration().Resources;
        var deletion = _context.Service.PreviewResourceDeletion(Node(Border)).AsSuccess.Result;
        Assert.True((await _context.Service.ApplyResourceDeletionAsync(deletion, ProjectFixtures.DefaultPalette)).HasSucceeded);
        MarkEdited(Node(MapA).DiskLocation!);
        await _context.ReopenAsync(RepointMapA);
        DropLastSecondaryPalette();

        await GenerateAsync(Plan(), confirmRemovals: true);

        var resources = ReadGeneration().Resources;
        Assert.Equal(first.Single(x => x.Key == Border), resources.Single(x => x.Key == Border));
        Assert.Equal(first.Single(x => x.Key == MapA), resources.Single(x => x.Key == MapA));
        Assert.DoesNotContain(resources, x => x.Key == Palette12);
        Assert.Equal(first.Count - 1, resources.Count);
        Assert.All(resources.Where(x => x.Key is not (Border or MapA)), x =>
        {
            Assert.NotNull(x.Path);
            Assert.Equal(TableManifest.HashFile(Node(x.Key).DiskLocation!), x.Hash);
        });
    }

    private const string General = "/Generated/Tilesets/General";
    private const string GeneralTiles = "/Generated/Tilesets/Compressed/General Tiles";

    private static RegeneratedResource Regenerated(GenerationPlan plan, string targetKey) =>
        Assert.Single(plan.Regeneration!.Resources, x => x.TargetKey == targetKey);

    [Fact]
    public async Task Regenerate_WrittenMapReferencesRebuiltSet()
    {
        await GenerateAsync();
        await _context.ReopenAsync(rom =>
        {
            FireRedShapedData.RepointPrimaryTiles(rom);
            RepointMapA(rom);
        });

        var plan = Plan();
        Assert.Equal(RegenerationKind.Changed, Regenerated(plan, GeneralTiles).Kind);
        Assert.Equal(RegenerationKind.Unchanged, Regenerated(plan, General).Kind);
        Assert.Equal(RegenerationKind.Changed, Regenerated(plan, MapA).Kind);

        await GenerateAsync(plan);

        var map = (MetatileMap)Node(MapA).Item;
        Assert.Same(Node(General).Item, map.Sets[0].Set);
        Assert.Same(Node(GeneralTiles).Item, map.Sets[0].Set.TileSet.Source);
        map.Composite();
        var save = await _context.Service.SaveProjectAsync(_context.Tree);
        Assert.True(save.HasSucceeded, save.HasFailed ? save.AsError.Reason : null);
    }

    [Fact]
    public async Task Regenerate_PaletteOnReplacedCompressedData_StillReadsColors()
    {
        await GenerateAsync();
        var tiles = (DataSource)Node(GeneralTiles).Item;
        var user = new Palette("User", new ColorFactory(), ColorModel.Bgr15, [new FileColorSource(0, Endian.Little)], false, PaletteStorageSource.ProjectXml, tiles);
        Assert.True((await _context.Service.AddResourceAsync(_context.Tree.Root, user)).HasSucceeded);
        await _context.ReopenAsync(FireRedShapedData.RepointPrimaryTiles);
        var palette = (Palette)Node("/User").Item;

        var plan = Plan();
        Assert.Equal(RegenerationKind.Changed, Regenerated(plan, GeneralTiles).Kind);
        await GenerateAsync(plan);

        var rebuilt = Assert.IsType<Palette>(Node("/User").Item);
        Assert.NotSame(palette, rebuilt);
        Assert.Same(Node(GeneralTiles).Item, rebuilt.DataSource);
        rebuilt.Reload();
        Assert.Equal(1, rebuilt.Entries);
        _ = rebuilt[0];
        Assert.Throws<ObjectDisposedException>(() => palette.DataSource!.Read(BitAddress.Zero, 8, new byte[1]));

        var saveResource = await _context.Service.SaveResourceAsync(_context.Tree, Node("/User"), true);
        Assert.True(saveResource.HasSucceeded, saveResource.HasFailed ? saveResource.AsError.Reason : null);
        var saveProject = await _context.Service.SaveProjectAsync(_context.Tree);
        Assert.True(saveProject.HasSucceeded, saveProject.HasFailed ? saveProject.AsError.Reason : null);
    }

    [Fact]
    public async Task Regenerate_MovedPalette_DependentsNotEdited()
    {
        await GenerateAsync();
        var setFile = Node(PalletSet).DiskLocation!;
        var before = File.ReadAllBytes(setFile);
        await MoveAsync(Palette12, "/Generated");
        Assert.NotEqual(before, File.ReadAllBytes(setFile));

        var regeneration = Plan().Regeneration!;

        Assert.False(Assert.Single(regeneration.Resources, x => x.Planned.Key == PalletSet).Entry!.IsEdited);
        Assert.All(regeneration.Resources, x => Assert.Equal(RegenerationKind.Unchanged, x.Kind));
        Assert.Empty(regeneration.Kept);
        Assert.Empty(regeneration.Removed);
    }

    [Fact]
    public async Task Regenerate_MovedPaletteThenEditedSet_StillEdited()
    {
        await GenerateAsync();
        await MoveAsync(Palette12, "/Generated");
        var setFile = Node(PalletSet).DiskLocation!;
        File.WriteAllText(setFile, File.ReadAllText(setFile).Replace("<metatileset", "<!-- edited -->\n<metatileset"));

        var regeneration = Plan().Regeneration!;

        Assert.True(Assert.Single(regeneration.Resources, x => x.Planned.Key == PalletSet).Entry!.IsEdited);
        Assert.False(Assert.Single(regeneration.Resources, x => x.Planned.Key == MapA).Entry!.IsEdited);
    }

    [Fact]
    public async Task Regenerate_MovedSetThenEditedMap_StillEdited()
    {
        await GenerateAsync();
        await MoveAsync(PalletSet, "/Generated");
        MarkEdited(Node(MapA).DiskLocation!);
        await _context.ReopenAsync(RepointMapA);

        var regeneration = Plan().Regeneration!;

        Assert.Contains($"Kept edited {MapA}", regeneration.SummaryLines(false));
        var border = Assert.Single(regeneration.Resources, x => x.Planned.Key == Border);
        Assert.Equal((RegenerationKind.Unchanged, false), (border.Kind, border.Entry!.IsEdited));
    }

    [Fact]
    public async Task Regenerate_UnchangedResourceFileDeleted_WritesEntryWithoutHash()
    {
        await GenerateAsync();
        File.Delete(Node(Palette12).DiskLocation!);

        var plan = Plan();
        Assert.Equal(RegenerationKind.Unchanged, Regenerated(plan, Palette12).Kind);
        await GenerateAsync(plan);

        Assert.Null(ReadGeneration().Resources.Single(x => x.Key == Palette12).Hash);
        Assert.NotNull(ReadGeneration().Resources.Single(x => x.Key == MapA).Hash);
    }

    private const string Pal1 = "/Monsters/Monster Palettes/MonsterPal1";
    private const string Pal3 = "/Monsters/Monster Palettes/MonsterPal3";
    private const string Pal4 = "/Monsters/Monster Palettes/MonsterPal4";

    private string Ff4File => Path.Combine(_context.Directory, "ff4.bin");

    private async Task GenerateFf4Async(Action<byte[]>? editRom = null)
    {
        var rom = Ff4ShapedData.Create();
        editRom?.Invoke(rom);
        File.WriteAllBytes(Ff4File, rom);
        Assert.True((await _context.Service.AddResourceAsync(_context.Tree.Root, new FileDataSource("Ff4", Ff4File))).HasSucceeded);
        await GenerateAsync(PlanFf4());
    }

    private async Task ReopenFf4Async(Action<byte[]> editRom)
    {
        _context.Service.CloseProjects();
        var rom = File.ReadAllBytes(Ff4File);
        editRom(rom);
        File.WriteAllBytes(Ff4File, rom);
        await _context.ReopenAsync();
    }

    private GenerationPlan PlanFf4()
    {
        var definition = _context.Definitions.Load("Ff4Synthetic.table.xml").AsSuccess.Result;
        var result = _context.Service.PlanGeneration(_context.Tree, "/Ff4", definition, "/Monsters");
        Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
        return result.AsSuccess.Result;
    }

    private Palette PaletteOf(string arrangerKey) =>
        ((IIndexedCodec)((ScatteredArranger)Node(arrangerKey).Item).GetElement(0, 0)!.Value.Codec).Palette;

    [Fact]
    public async Task Regenerate_DependentReferencesResourceNowAtOldKey()
    {
        await GenerateFf4Async(rom => Ff4ShapedData.SetBossPalette(rom, 0, 3));
        Assert.Same(Node(Pal3).Item, PaletteOf("/Monsters/02 Golbez"));
        await ReopenFf4Async(rom => Ff4ShapedData.SetMonsterPalette(rom, 5, 4));

        var plan = PlanFf4();
        var renamed = Assert.Single(plan.Regeneration!.Resources, x => x.Entry?.Entry.Key == Pal3);
        Assert.Equal((RegenerationKind.Changed, Pal4), (renamed.Kind, renamed.TargetKey));
        Assert.Equal(RegenerationKind.Added, Regenerated(plan, Pal3).Kind);
        Assert.Equal(RegenerationKind.Unchanged, Regenerated(plan, "/Monsters/02 Golbez").Kind);
        Assert.Empty(_context.Service.FindGenerationConflicts(_context.Tree, plan));

        await GenerateAsync(plan);

        Assert.Same(Node(Pal3).Item, PaletteOf("/Monsters/02 Golbez"));
        Assert.Same(Node(Pal4).Item, PaletteOf("/Monsters/05 Mt.Dr.Ab"));
    }

    [Fact]
    public async Task Regenerate_RecordSwitchesToPaletteAnotherUses_PalettesUnchanged()
    {
        await GenerateFf4Async();
        await ReopenFf4Async(rom => Ff4ShapedData.SetMonsterPalette(rom, 0, 3));
        var pal3 = Node(Pal3).Item;

        var plan = PlanFf4();
        Assert.Empty(_context.Service.FindGenerationConflicts(_context.Tree, plan));
        Assert.All(plan.Regeneration!.Resources.Where(x => x.Planned.Type == "palette"), x => Assert.Equal(RegenerationKind.Unchanged, x.Kind));
        Assert.Empty(plan.Regeneration.Removed);
        Assert.Equal(["re-paletted"], Regenerated(plan, "/Monsters/00 Imp").ChangeKinds);

        await GenerateAsync(plan);

        Assert.Same(pal3, Node(Pal3).Item);
        Assert.Same(pal3, PaletteOf("/Monsters/00 Imp"));
        Assert.Same(Node(Pal1).Item, PaletteOf("/Monsters/01 Bomb"));
    }

    [Fact]
    public async Task Regenerate_RecordSwitchesToUnusedPalette_AddsIt()
    {
        await GenerateFf4Async();
        await ReopenFf4Async(rom => Ff4ShapedData.SetMonsterPalette(rom, 0, 4));
        var pal1 = Node(Pal1).Item;

        var plan = PlanFf4();
        Assert.Equal(RegenerationKind.Added, Regenerated(plan, Pal4).Kind);
        Assert.Equal(RegenerationKind.Unchanged, Regenerated(plan, Pal1).Kind);

        await GenerateAsync(plan);

        Assert.Same(Node(Pal4).Item, PaletteOf("/Monsters/00 Imp"));
        Assert.Same(pal1, PaletteOf("/Monsters/01 Bomb"));
    }

    [Fact]
    public async Task Regenerate_ArrangerReferencingDeletedPalette_KeptAsIs()
    {
        const string bomb = "/Monsters/01 Bomb";
        await GenerateFf4Async();
        var deletion = _context.Service.PreviewResourceDeletion(Node(Pal1)).AsSuccess.Result;
        Assert.True((await _context.Service.ApplyResourceDeletionAsync(deletion, ProjectFixtures.DefaultPalette)).HasSucceeded);
        var bombFile = Node(bomb).DiskLocation!;
        var bytes = File.ReadAllBytes(bombFile);
        var plan = PlanFf4();
        var entry = ReadGeneration(plan.ManifestFileName, "/Monsters").Resources.Single(x => x.Key == bomb);
        Assert.Contains($"Kept {bomb}: references deleted {Pal1}", plan.Regeneration!.SummaryLines(false));
        Assert.Contains($"Kept missing {Pal1}", plan.Regeneration.SummaryLines(false));

        await GenerateAsync(plan);

        Assert.Equal(bytes, File.ReadAllBytes(bombFile));
        Assert.Equal(entry, ReadGeneration(plan.ManifestFileName, "/Monsters").Resources.Single(x => x.Key == bomb));
        Assert.False(_context.Tree.TryGetNode(Pal1, out _));
    }
}
