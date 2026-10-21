using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.Tables;
using TileShop.Shared.Interactions;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Dialogs;

public sealed class GenerateResourcesViewModelTests : IAsyncLifetime
{
    private readonly GenerationProjectContext _context = new(f => new GatedProjectService(f));
    private readonly StubFileRequestService _files = new();

    private GatedProjectService Service => (GatedProjectService)_context.Service;

    public Task InitializeAsync() => _context.InitializeAsync();

    public Task DisposeAsync()
    {
        Service.Gate.Set();
        _context.Dispose();
        return Task.CompletedTask;
    }

    private GenerateResourcesViewModel Create(string? definitionPath = null)
    {
        var vm = new GenerateResourcesViewModel(Service, _context.Tree, "/Rom", "/Generated", _files);
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
        Assert.Equal(vm.Plan!.Resources, vm.FilteredOutputs);
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

    private sealed class GatedProjectService(IProjectSerializerFactory serializerFactory) : ProjectService(serializerFactory)
    {
        public ManualResetEventSlim Gate { get; } = new(true);

        public override MagitekResult<GenerationPlan> PlanGeneration(ProjectTree tree, string dataFileKey, TableDefinition definition, string targetFolderKey)
        {
            Gate.Wait(TimeSpan.FromSeconds(10));
            return base.PlanGeneration(tree, dataFileKey, definition, targetFolderKey);
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
