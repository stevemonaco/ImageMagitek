using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.Services.Stores;
using ImageMagitek.Tables;
using ImageMagitek.UnitTests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using TileShop.Shared.Interactions;
using TileShop.Shared.Services;
using TileShop.UI.Models;
using TileShop.UI.Services;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Dialogs;

public sealed class GenerateResourcesViewModelTests : IAsyncLifetime
{
    private readonly GenerationProjectContext _context = new(f => new GatedProjectService(f));
    private readonly StubFileRequestService _files = new();
    private readonly ScriptedInteractionService _interactions = new();

    private GatedProjectService Service => (GatedProjectService)_context.Service;

    public Task InitializeAsync() => _context.InitializeAsync();

    public Task DisposeAsync()
    {
        Service.Gate.Set();
        _context.Dispose();
        return Task.CompletedTask;
    }

    private GenerateResourcesViewModel Create(string? definitionPath = null, EditorsViewModel? editors = null, string dataFileKey = "/Rom",
        string folderKey = "/Generated")
    {
        var vm = new GenerateResourcesViewModel(Service, _context.Tree, dataFileKey, folderKey, _files, _interactions, editors);
        vm.DefinitionPath = definitionPath ?? _context.DefinitionFile;
        return vm;
    }

    private static void AssertNoPreview(GenerateResourcesViewModel vm)
    {
        Assert.False(vm.HasPreview);
        Assert.Empty(vm.FilteredOutputs);
        Assert.Equal("", vm.Summary);
        Assert.False(vm.TryAcceptCommand.CanExecute(null));
    }

    [Fact]
    public async Task Preview_Succeeds_ShowsSummaryOutputsAndEnablesGenerate()
    {
        var vm = Create();
        Assert.Equal("/Generated", vm.TargetFolder);
        Assert.False(vm.TryAcceptCommand.CanExecute(null));
        var before = _context.Snapshot();

        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.Null(vm.GenerationErrorText);
        Assert.False(vm.IsPreviewing);
        Assert.Equal(20, vm.FilteredOutputs.Count);
        Assert.Equal(vm.Plan!.Resources.Select(x => (x.Key, x.Type)), vm.FilteredOutputs.Select(x => (x.Key, x.Type)));
        Assert.Empty(vm.Conflicts);
        var counts = vm.FilteredOutputs.GroupBy(x => x.Type).Select(x => $"Would create {x.Count()} {x.Key}");
        Assert.Equal(string.Join("\n", counts), vm.Summary);
        Assert.Equal(vm.FilteredOutputs.Select(x => x.Type).Distinct().Prepend(GenerateResourcesViewModel.AllTypes), vm.TypeFilters);
        Assert.Equal(GenerateResourcesViewModel.AllTypes, vm.SelectedTypeFilter);
        Assert.True(vm.TryAcceptCommand.CanExecute(null));
        _context.AssertUnchanged(before);

        vm.SelectedTypeFilter = "metatilemap";

        Assert.NotEmpty(vm.FilteredOutputs);
        Assert.All(vm.FilteredOutputs, x => Assert.Equal("metatilemap", x.Type));
        Assert.Contains(vm.FilteredOutputs, x => x.Key == "/Generated/Maps/Pallet Town/Pallet Town");
    }

    [Fact]
    public async Task Browse_FillsDefinitionPath()
    {
        var vm = Create("");
        Assert.False(vm.PreviewCommand.CanExecute(null));
        _files.TableDefinition = new Uri(_context.DefinitionFile);

        await vm.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(_context.DefinitionFile, vm.DefinitionPath);
        Assert.True(vm.PreviewCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("/Bad:Name", "is not a valid folder name")]
    [InlineData("/Rom", "'/Rom' is not a folder")]
    public async Task Preview_InvalidFolder_ShowsErrorKeepsGenerateDisabled(string folder, string expected)
    {
        var vm = Create();
        vm.TargetFolder = folder;

        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.Contains(expected, vm.GenerationErrorText);
        AssertNoPreview(vm);
    }

    [Fact]
    public async Task Preview_DefinitionLoadError_ShowsErrorKeepsGenerateDisabled()
    {
        var missing = Path.Combine(_context.Definitions.Directory, "Missing.table.xml");
        var vm = Create(missing);

        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.StartsWith($"Definition '{missing}' contained 1 errors\n1: ", vm.GenerationErrorText);
        Assert.Contains("does not exist", vm.GenerationErrorText);
        AssertNoPreview(vm);

        vm.DefinitionPath = _context.Definitions.Write("Malformed.table.xml", "<tableDefinition");
        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.StartsWith($"Definition '{vm.DefinitionPath}' contained 1 errors\n1: ", vm.GenerationErrorText);
        AssertNoPreview(vm);
    }

    [Fact]
    public async Task Preview_Conflict_ListsItAndBlocksGenerate()
    {
        var folder = (await Service.CreateNewFolderAsync(_context.Tree.Root, "Generated")).AsSuccess.Result;
        var tilesets = (await Service.CreateNewFolderAsync(folder, "Tilesets")).AsSuccess.Result;
        var rom = (DataSource)_context.Tree.Root.ChildNodes.Single(x => x.Name == "Rom").Item;
        var palette = new Palette("general", new ColorFactory(), ColorModel.Bgr15, [new FileColorSource(0, Endian.Little)], false, PaletteStorageSource.ProjectXml, rom);
        Assert.True((await Service.AddResourceAsync(tilesets, palette)).HasSucceeded);
        var vm = Create();

        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.True(vm.HasPreview);
        Assert.NotEmpty(vm.Conflicts);
        Assert.Contains("'/Generated/Tilesets/General' clashes with '/Generated/Tilesets/general'", vm.Conflicts);
        Assert.False(vm.TryAcceptCommand.CanExecute(null));
    }

    [Fact]
    public async Task ChangingInputs_InvalidatesPreview()
    {
        var vm = Create();
        await vm.PreviewCommand.ExecuteAsync(null);
        Assert.True(vm.TryAcceptCommand.CanExecute(null));

        vm.TargetFolder = "/Other";

        AssertNoPreview(vm);

        await vm.PreviewCommand.ExecuteAsync(null);
        Assert.Equal("/Other", vm.ProduceResult());

        vm.DefinitionPath = _context.DefinitionFile + " ";

        AssertNoPreview(vm);

        Service.Gate.Reset();
        var preview = vm.PreviewCommand.ExecuteAsync(null);
        Assert.True(vm.IsPreviewing);
        Assert.False(vm.PreviewCommand.CanExecute(null));
        vm.TargetFolder = "/Late";
        Service.Gate.Set();
        await preview;

        Assert.False(vm.IsPreviewing);
        AssertNoPreview(vm);
    }

    [Fact]
    public async Task Generate_CallsServiceAndReturnsTargetFolder()
    {
        var vm = Create();
        var closed = false;
        vm.Closed += (_, _) => closed = true;
        await vm.PreviewCommand.ExecuteAsync(null);

        await vm.TryAcceptCommand.ExecuteAsync(null);

        Assert.True(closed);
        Assert.Null(vm.GenerationErrorText);
        Assert.False(vm.IsGenerating);
        Assert.Equal("/Generated", vm.RequestResult);
        Assert.True(_context.Tree.TryGetItem<MetatileMap>("/Generated/Maps/Pallet Town/Pallet Town", out _));
        Assert.True(File.Exists(_context.ManifestFile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generate_PassesSaveProjectOption(bool saveProject)
    {
        var vm = Create();
        Assert.False(vm.SaveProject);
        vm.SaveProject = saveProject;
        await vm.PreviewCommand.ExecuteAsync(null);

        await vm.TryAcceptCommand.ExecuteAsync(null);

        Assert.Equal(saveProject, Service.LastSaveProject);
    }

    [Fact]
    public async Task Generate_ServiceFails_ShowsReasonAndStaysOpen()
    {
        var vm = Create();
        var closed = false;
        vm.Closed += (_, _) => closed = true;
        await vm.PreviewCommand.ExecuteAsync(null);
        Assert.True((await Service.CreateNewFolderAsync(_context.Tree.Root, "generated")).HasSucceeded);

        await vm.TryAcceptCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Contains("clashes with", vm.GenerationErrorText);
        Assert.False(File.Exists(_context.ManifestFile));
        Assert.True(vm.TryCancelCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancel_WritesNothing()
    {
        var vm = Create();
        await vm.PreviewCommand.ExecuteAsync(null);
        var before = _context.Snapshot();

        await vm.TryCancelCommand.ExecuteAsync(null);

        Assert.Null(vm.RequestResult);
        _context.AssertUnchanged(before);
        Assert.False(_context.Tree.TryGetNode("/Generated", out _));
    }

    private const string MapA = "/Generated/Maps/Pallet Town/Pallet Town";
    private const string Palette12 = "/Generated/Tilesets/Palettes/Pallet Town 12";
    private const string General0 = "/Generated/Tilesets/Palettes/General 0";

    private async Task GenerateFirstAsync()
    {
        var definition = _context.Definitions.Load("Synthetic.table.xml").AsSuccess.Result;
        var plan = Service.PlanGeneration(_context.Tree, "/Rom", definition, "/Generated").AsSuccess.Result;
        Assert.True((await Service.GenerateResourcesAsync(_context.Tree, plan)).HasSucceeded);
    }

    private Task RepointMapAsync() =>
        _context.ReopenAsync(rom => FireRedShapedData.RepointLayoutBlocks(rom, FireRedShapedData.LayoutA, FireRedShapedData.FreeSpace));

    private void DropLastSecondaryPalette() =>
        _context.EditDefinition(x => x.Replace("repeat=\"isSecondary ? 6 : 7\"", "repeat=\"isSecondary ? 5 : 7\""));

    private void MakePalettesOpaque() =>
        _context.EditDefinition(x => x.Replace("zeroindextransparent=\"true\"", "zeroindextransparent=\"false\""));

    private async Task<ScatteredArranger> AddUserArrangerAsync(Palette palette)
    {
        var codec = (IIndexedCodec)CodecFixture.Shared.CodecFactory.CreateCodec("GBA 4bpp")!;
        codec.Palette = palette;
        var arranger = new ScatteredArranger("User", PixelColorType.Indexed, ElementLayout.Tiled, 1, 1, codec.Width, codec.Height);
        arranger.SetElement(new ArrangerElement(0, 0, (DataSource)Item("/Rom"), BitAddress.Zero, codec), 0, 0);
        Assert.True((await Service.AddResourceAsync(_context.Tree.Root, arranger)).HasSucceeded);
        return arranger;
    }

    private async Task<GenerateResourcesViewModel> PreviewRemovalAsync()
    {
        await GenerateFirstAsync();
        DropLastSecondaryPalette();
        var vm = Create();
        await vm.PreviewCommand.ExecuteAsync(null);
        Assert.Contains(vm.FilteredOutputs, x => (x.Key, x.Change) == (Palette12, "removed"));
        return vm;
    }

    private EditorsViewModel CreateEditors()
    {
        var preferences = new UserPreferencesStore(Path.Combine(_context.Directory, "preferences.json"), NullLogger<UserPreferencesStore>.Instance);
        return new EditorsViewModel(SettingsService.CreateDefault(), _interactions, preferences, CodecFixture.Shared.CodecService, new ColorFactory(),
            new PaletteStore(ProjectFixtures.DefaultPalette, [ProjectFixtures.DefaultPalette]), Service, new ElementStore(), NullLoggerFactory.Instance,
            new HotkeyService(), null!, new ClipboardService());
    }

    [Fact]
    public async Task Regeneration_PreviewShowsChanges()
    {
        await GenerateFirstAsync();
        await RepointMapAsync();
        var vm = Create();
        Assert.Equal("Generate", vm.AcceptName);

        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.True(vm.IsRegeneration);
        Assert.Equal("Regenerate", vm.AcceptName);
        Assert.Contains("Would change 1 metatilemap", vm.Summary);
        Assert.Contains($"Would change {MapA}: repointed", vm.Summary);
        Assert.Contains("Unchanged 13 palette", vm.Summary);
        Assert.Equal(20, vm.FilteredOutputs.Count);
        Assert.Equal("changed: repointed", Assert.Single(vm.FilteredOutputs, x => x.Key == MapA).Change);
        Assert.All(vm.FilteredOutputs.Where(x => x.Key != MapA), x => Assert.Equal("unchanged", x.Change));
        Assert.True(vm.TryAcceptCommand.CanExecute(null));
    }

    [Fact]
    public async Task OverwriteEdited_ClearsPreviewAndIsPassed()
    {
        await GenerateFirstAsync();
        var vm = Create();
        Assert.False(vm.OverwriteEdited);
        await vm.PreviewCommand.ExecuteAsync(null);
        Assert.True(vm.HasPreview);
        Assert.Equal(false, Service.LastOverwriteEdited);

        vm.OverwriteEdited = true;

        AssertNoPreview(vm);
        Assert.True(vm.IsRegeneration);

        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.Equal(true, Service.LastOverwriteEdited);
        Assert.True(vm.HasPreview);

        vm.TargetFolder = "/Other";

        Assert.False(vm.IsRegeneration);
    }

    [Fact]
    public async Task Removals_ConfirmedBeforeRegenerating()
    {
        var vm = await PreviewRemovalAsync();
        var closed = false;
        vm.Closed += (_, _) => closed = true;
        _interactions.Answers.Enqueue(PromptResult.Accept);

        await vm.TryAcceptCommand.ExecuteAsync(null);

        Assert.True(closed);
        var message = Assert.Single(_interactions.Messages);
        Assert.StartsWith("Remove 1 resources?", message);
        Assert.Contains(Palette12, message);
        Assert.Equal(true, Service.LastConfirmRemovals);
        Assert.False(_context.Tree.TryGetNode(Palette12, out _));
    }

    [Fact]
    public async Task Removals_Declined_WritesNothing()
    {
        var vm = await PreviewRemovalAsync();
        var closed = false;
        vm.Closed += (_, _) => closed = true;
        var before = _context.Snapshot();
        var generateCount = Service.GenerateCount;
        _interactions.Answers.Enqueue(PromptResult.Reject);

        await vm.TryAcceptCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Equal(1, _interactions.PromptCount);
        Assert.Equal(generateCount, Service.GenerateCount);
        _context.AssertUnchanged(before);
        Assert.True(_context.Tree.TryGetNode(Palette12, out _));
        Assert.True(vm.TryAcceptCommand.CanExecute(null));
    }

    [Fact]
    public async Task Regenerate_ClosesRemovedAndReloadsChangedEditors()
    {
        await GenerateFirstAsync();
        var editors = CreateEditors();
        Assert.True(_context.Tree.TryGetNode(General0, out var changedNode));
        Assert.True(_context.Tree.TryGetNode(Palette12, out var removedNode));
        await editors.ActivateEditor(removedNode.Item);
        await editors.ActivateEditor(changedNode.Item);
        MakePalettesOpaque();
        DropLastSecondaryPalette();
        var vm = Create(editors: editors);
        await vm.PreviewCommand.ExecuteAsync(null);
        Assert.Contains("changed", Assert.Single(vm.FilteredOutputs, x => x.Key == General0).Change);
        _interactions.Answers.Enqueue(PromptResult.Accept);

        await vm.TryAcceptCommand.ExecuteAsync(null);

        Assert.True(_context.Tree.TryGetNode(General0, out var regenerated));
        Assert.NotSame(changedNode.Item, regenerated.Item);
        var editor = Assert.Single(editors.Editors);
        Assert.Same(regenerated.Item, editor.Resource);
        Assert.Same(editor, editors.ActiveEditor);
        Assert.False(((Palette)regenerated.Item).ZeroIndexTransparent);
    }

    private IProjectResource Item(string key)
    {
        Assert.True(_context.Tree.TryGetNode(key, out var node), $"'{key}' is not in the tree");
        return node.Item;
    }

    private async Task RegenerateAsync(EditorsViewModel editors)
    {
        var vm = Create(editors: editors);
        await vm.PreviewCommand.ExecuteAsync(null);
        await vm.TryAcceptCommand.ExecuteAsync(null);
        Assert.Null(vm.GenerationErrorText);
    }

    [Fact]
    public async Task Regenerate_ReopensEditorOfArrangerUsingReplacedPalette()
    {
        await GenerateFirstAsync();
        var palette = (Palette)Item(General0);
        var arranger = await AddUserArrangerAsync(palette);
        var editors = CreateEditors();
        await editors.ActivateEditor(arranger);
        var stale = Assert.Single(editors.Editors);
        MakePalettesOpaque();

        await RegenerateAsync(editors);

        var regenerated = Item(General0);
        Assert.NotSame(palette, regenerated);
        var editor = Assert.IsType<GraphicsEditorViewModel>(Assert.Single(editors.Editors));
        Assert.NotSame(stale, editor);
        Assert.Same(arranger, editor.Resource);
        Assert.Same(regenerated, ((IIndexedCodec)editor.WorkingArranger.GetElement(0, 0)!.Value.Codec).Palette);
        Assert.Contains(editor.Palettes, x => ReferenceEquals(x.Palette, regenerated));
        Assert.DoesNotContain(editor.Palettes, x => ReferenceEquals(x.Palette, palette));
        Assert.Same(editor, editors.ActiveEditor);
    }

    [Fact]
    public async Task Regenerate_ReopensSequentialEditorOfChangedCompressedData()
    {
        const string generalTiles = "/Generated/Tilesets/Compressed/General Tiles";
        await GenerateFirstAsync();
        await _context.ReopenAsync(FireRedShapedData.RepointPrimaryTiles);
        var tiles = Item(generalTiles);
        var editors = CreateEditors();
        await editors.ActivateEditor(tiles);
        Assert.Same(tiles, Assert.Single(editors.Editors).OriginatingProjectResource);

        await RegenerateAsync(editors);

        var regenerated = Item(generalTiles);
        Assert.NotSame(tiles, regenerated);
        var editor = Assert.Single(editors.Editors);
        Assert.Same(regenerated, editor.OriginatingProjectResource);
        Assert.Same(editor, editors.ActiveEditor);
    }

    private async Task<(GenerateResourcesViewModel Vm, TestEditor Editor)> PreviewWithModifiedEditorAsync()
    {
        await GenerateFirstAsync();
        var editors = CreateEditors();
        Assert.True(_context.Tree.TryGetNode(General0, out var node));
        var editor = new TestEditor(node.Item, "General 0", () => File.AppendAllText(node.DiskLocation!, "<!-- saved -->")) { IsModified = true };
        editors.Editors.Add(editor);
        MakePalettesOpaque();
        var vm = Create(editors: editors);
        await vm.PreviewCommand.ExecuteAsync(null);
        Assert.StartsWith("changed", Assert.Single(vm.FilteredOutputs, x => x.Key == General0).Change);
        return (vm, editor);
    }

    [Fact]
    public async Task Regenerate_ModifiedEditorCancelled_WritesNothing()
    {
        var (vm, editor) = await PreviewWithModifiedEditorAsync();
        var closed = false;
        vm.Closed += (_, _) => closed = true;
        var before = _context.Snapshot();
        var generateCount = Service.GenerateCount;
        _interactions.Answers.Enqueue(PromptResult.Cancel);

        await vm.TryAcceptCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Equal("'General 0' has been modified and will be closed by regeneration. Save changes?", Assert.Single(_interactions.Messages));
        Assert.Equal(generateCount, Service.GenerateCount);
        _context.AssertUnchanged(before);
        Assert.True(editor.IsModified);
    }

    [Fact]
    public async Task Regenerate_ModifiedEditorDiscarded_Regenerates()
    {
        var (vm, editor) = await PreviewWithModifiedEditorAsync();
        var closed = false;
        vm.Closed += (_, _) => closed = true;
        var generateCount = Service.GenerateCount;
        _interactions.Answers.Enqueue(PromptResult.Reject);

        await vm.TryAcceptCommand.ExecuteAsync(null);

        Assert.True(closed);
        Assert.False(editor.IsModified);
        Assert.Equal(0, editor.SaveCount);
        Assert.Equal(generateCount + 1, Service.GenerateCount);
        Assert.False(((Palette)Item(General0)).ZeroIndexTransparent);
    }

    [Fact]
    public async Task Regenerate_ModifiedEditorSaved_RefreshesPreview()
    {
        var (vm, editor) = await PreviewWithModifiedEditorAsync();
        var closed = false;
        vm.Closed += (_, _) => closed = true;
        var generateCount = Service.GenerateCount;
        _interactions.Answers.Enqueue(PromptResult.Accept);

        await vm.TryAcceptCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Equal(1, editor.SaveCount);
        Assert.Equal(generateCount, Service.GenerateCount);
        Assert.True(vm.HasPreview);
        Assert.Equal("kept edited", Assert.Single(vm.FilteredOutputs, x => x.Key == General0).Change);
        Assert.True(vm.TryAcceptCommand.CanExecute(null));
    }

    [Fact]
    public async Task Regenerate_ModifiedEditorOfRetargetedArranger_Prompted()
    {
        await GenerateFirstAsync();
        var arranger = await AddUserArrangerAsync((Palette)Item(General0));
        var editors = CreateEditors();
        await editors.ActivateEditor(arranger);
        var stale = Assert.IsType<GraphicsEditorViewModel>(Assert.Single(editors.Editors));
        stale.IsModified = true;
        MakePalettesOpaque();
        _interactions.Answers.Enqueue(PromptResult.Reject);

        await RegenerateAsync(editors);

        Assert.Contains("has been modified and will be closed by regeneration", Assert.Single(_interactions.Messages));
        Assert.NotSame(stale, Assert.Single(editors.Editors));
    }

    private sealed class TestEditor : ResourceEditorBaseViewModel
    {
        private readonly Action _save;

        public int SaveCount { get; private set; }

        public TestEditor(IProjectResource resource, string name, Action save) : base(resource)
        {
            DisplayName = name;
            _save = save;
        }

        public override Task SaveChangesAsync()
        {
            _save();
            SaveCount++;
            IsModified = false;
            return Task.CompletedTask;
        }

        public override void DiscardChanges() => IsModified = false;
        public override void Undo() { }
        public override void Redo() { }
    }

    private sealed class GatedProjectService(IProjectSerializerFactory serializerFactory) : ProjectService(serializerFactory)
    {
        public ManualResetEventSlim Gate { get; } = new(true);
        public bool? LastSaveProject { get; private set; }
        public bool? LastOverwriteEdited { get; private set; }
        public bool? LastConfirmRemovals { get; private set; }
        public int GenerateCount { get; private set; }

        public override Task<MagitekResult> GenerateResourcesAsync(ProjectTree tree, GenerationPlan plan, bool saveProject = false, bool confirmRemovals = false)
        {
            LastSaveProject = saveProject;
            LastConfirmRemovals = confirmRemovals;
            GenerateCount++;
            return base.GenerateResourcesAsync(tree, plan, saveProject, confirmRemovals);
        }

        public override MagitekResult<GenerationPlan> PlanGeneration(ProjectTree tree, string dataFileKey, TableDefinition definition, string targetFolderKey,
            bool overwriteEdited = false)
        {
            Gate.Wait(TimeSpan.FromSeconds(10));
            LastOverwriteEdited = overwriteEdited;
            return base.PlanGeneration(tree, dataFileKey, definition, targetFolderKey, overwriteEdited);
        }
    }

    private sealed class StubFileRequestService : IAsyncFileRequestService
    {
        public Uri? TableDefinition { get; set; }

        public Task<Uri?> RequestTableDefinitionFileName() => Task.FromResult(TableDefinition);
        public Task<Uri?> RequestProjectFileName() => Task.FromResult<Uri?>(null);
        public Task<Uri?> RequestNewProjectFileName() => Task.FromResult<Uri?>(null);
        public Task<Uri?> RequestExistingDataFileName() => Task.FromResult<Uri?>(null);
        public Task<Uri?> RequestExportArrangerFileName(string defaultName) => Task.FromResult<Uri?>(null);
        public Task<Uri?> RequestImportArrangerFileName() => Task.FromResult<Uri?>(null);
        public Task<Uri?> RequestExportPaletteFileName(string defaultName) => Task.FromResult<Uri?>(null);
        public Task<Uri?> RequestImportPaletteFileName() => Task.FromResult<Uri?>(null);
        public Task<Uri?> RequestExportDataFileName(string defaultName) => Task.FromResult<Uri?>(null);
    }
}
