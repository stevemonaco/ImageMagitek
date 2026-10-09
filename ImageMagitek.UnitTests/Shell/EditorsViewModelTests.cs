using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.Services.Stores;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Microsoft.Extensions.Logging.Abstractions;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;
using TileShop.Shared.Services;
using TileShop.UI.Models;
using TileShop.UI.Services;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Shell;

public sealed class EditorsViewModelTests : IAsyncLifetime
{
    private readonly string _directory = TestPaths.CreateTempPath("");
    private readonly Palette _palette = ArrangerTestFactory.CreatePalette(new ColorRgba32(0, 0, 0, 255));
    private readonly ColorFactory _colorFactory = new();
    private readonly CountingProjectService _projectService;
    private readonly ScriptedInteractionService _interactions = new();
    private readonly EditorsViewModel _editors;
    private ProjectTree _tree = null!;

    public EditorsViewModelTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "rom.bin"), new byte[64]);

        var serializerFactory = new XmlProjectSerializerFactory(Path.Combine(AppContext.BaseDirectory, "_schemas", "ResourceSchema.xsd"),
            CodecFixture.Shared.CodecFactory, _colorFactory, [_palette]);
        _projectService = new CountingProjectService(serializerFactory);

        var preferences = new UserPreferencesStore(Path.Combine(_directory, "preferences.json"), NullLogger<UserPreferencesStore>.Instance);
        _editors = new EditorsViewModel(SettingsService.CreateDefault(), _interactions, preferences, CodecFixture.Shared.CodecService,
            _colorFactory, new PaletteStore(_palette, [_palette]), _projectService, new ElementStore(), NullLoggerFactory.Instance,
            new HotkeyService(), null!, new ClipboardService());
    }

    public async Task InitializeAsync()
    {
        _tree = (await _projectService.CreateNewProjectAsync(Path.Combine(_directory, "project.xml"))).AsSuccess.Result;
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

    [Fact]
    public async Task CloseEditor_Active_ActivatesFirstRemaining()
    {
        var a = await AddEditorAsync("A");
        var b = await AddEditorAsync("B");
        var c = await AddEditorAsync("C");
        _editors.ActiveEditor = c;

        Assert.True(await _editors.CloseEditor(c));

        Assert.Same(a, _editors.ActiveEditor);
        Assert.Equal([a, b], _editors.Editors);
    }

    [Fact]
    public async Task CloseEditor_Last_LeavesNoActiveEditor()
    {
        var a = await AddEditorAsync("A");
        _editors.ActiveEditor = a;

        await _editors.CloseEditor(a);

        Assert.Null(_editors.ActiveEditor);
        Assert.Same(EditCommands.None, _editors.ActiveEditCommands);
    }

    [Fact]
    public async Task CloseEditor_Inactive_KeepsActiveEditor()
    {
        var a = await AddEditorAsync("A");
        var b = await AddEditorAsync("B");
        _editors.ActiveEditor = b;

        await _editors.CloseEditor(a);

        Assert.Same(b, _editors.ActiveEditor);
    }

    [Fact]
    public async Task CloseEditor_Yes_SavesProjectOnce()
    {
        var a = await AddEditorAsync("A", isModified: true);
        _interactions.Answers.Enqueue(PromptResult.Accept);

        await _editors.CloseEditor(a);

        Assert.Equal(1, a.SaveCount);
        Assert.Equal(1, _projectService.SaveCount);
    }

    [Fact]
    public async Task RequestSaveAllUserChanges_TwoEditorsInOneProject_SavesProjectOnce()
    {
        var a = await AddEditorAsync("A", isModified: true);
        var b = await AddEditorAsync("B", isModified: true);
        _interactions.Answers.Enqueue(PromptResult.Accept);
        _interactions.Answers.Enqueue(PromptResult.Accept);

        Assert.True(await _editors.RequestSaveAllUserChanges());

        Assert.Equal((1, 1), (a.SaveCount, b.SaveCount));
        Assert.Equal(1, _projectService.SaveCount);
    }

    [Fact]
    public async Task RequestSaveAllUserChanges_Cancel_SavesNothing()
    {
        var a = await AddEditorAsync("A", isModified: true);
        await AddEditorAsync("B", isModified: true);
        _interactions.Answers.Enqueue(PromptResult.Cancel);

        Assert.False(await _editors.RequestSaveAllUserChanges());

        Assert.Equal(0, a.SaveCount);
        Assert.Equal(0, _projectService.SaveCount);
    }

    [Fact]
    public async Task SaveAllAsync_SavesWithoutPromptingOncePerProject()
    {
        var a = await AddEditorAsync("A", isModified: true);
        var b = await AddEditorAsync("B", isModified: true);
        var unmodified = await AddEditorAsync("C");
        Assert.True(_editors.HasModifiedEditors);

        await _editors.SaveAllAsync();

        Assert.Equal((1, 1, 0), (a.SaveCount, b.SaveCount, unmodified.SaveCount));
        Assert.Equal(1, _projectService.SaveCount);
        Assert.Equal(0, _interactions.PromptCount);
        Assert.False(_editors.HasModifiedEditors);
    }

    [Fact]
    public async Task Rename_DataFile_RetitlesEditorOpenedFromIt()
    {
        var node = await AddDataFileAsync("rom");
        var editor = new TestEditor(new ResourceFolder("Working"), "rom") { OriginatingProjectResource = node.Item };
        _editors.Editors.Add(editor);

        Assert.True((await _projectService.RenameResourceAsync(node, "renamed")).HasSucceeded);

        Assert.Equal("renamed", editor.DisplayName);
    }

    private async Task<TestEditor> AddEditorAsync(string name, bool isModified = false)
    {
        var node = await AddDataFileAsync(name);
        var editor = new TestEditor(node.Item, name) { IsModified = isModified };
        _editors.Editors.Add(editor);
        return editor;
    }

    private async Task<ResourceNode> AddDataFileAsync(string name) =>
        (await _projectService.AddResourceAsync(_tree.Root, new FileDataSource(name, Path.Combine(_directory, "rom.bin")))).AsSuccess.Result;

    private sealed class CountingProjectService(IProjectSerializerFactory serializerFactory) : ProjectService(serializerFactory)
    {
        public int SaveCount { get; private set; }

        public override Task<MagitekResult> SaveProjectAsync(ProjectTree projectTree)
        {
            SaveCount++;
            return base.SaveProjectAsync(projectTree);
        }
    }

    private sealed class TestEditor : ResourceEditorBaseViewModel
    {
        public int SaveCount { get; private set; }

        public TestEditor(IProjectResource resource, string name) : base(resource)
        {
            DisplayName = name;
        }

        public override Task SaveChangesAsync()
        {
            SaveCount++;
            IsModified = false;
            return Task.CompletedTask;
        }

        public override void DiscardChanges() => IsModified = false;
        public override void Undo() { }
        public override void Redo() { }
        public override void ApplyHistoryAction(HistoryAction action) { }
    }

    private sealed class ScriptedInteractionService : IInteractionService
    {
        public Queue<PromptResult> Answers { get; } = new();
        public int PromptCount { get; private set; }

        public Task AlertAsync(string heading, string message) => Task.CompletedTask;

        public Task<PromptResult> PromptAsync(PromptChoice choices, string heading, string message)
        {
            PromptCount++;
            return Task.FromResult(Answers.Dequeue());
        }

        public Task<TResult?> RequestAsync<TResult>(IRequestMediator<TResult> mediator) => throw new NotSupportedException();
    }
}
