using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Codec;
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

    [Fact]
    public async Task Replaced_ReopensEditorInTabPositionAndKeepsActive()
    {
        var a = await AddEditorAsync("A");
        var paletteNode = new PaletteNode("Pal", ArrangerTestFactory.CreatePalette(new ColorRgba32(1, 2, 3, 255)));
        _tree.Root.AttachChildNode(paletteNode);
        await _editors.ActivateEditor(paletteNode.Item);
        var stale = _editors.ActiveEditor!;
        var c = await AddEditorAsync("C");
        var replacement = new PaletteNode("Pal", ArrangerTestFactory.CreatePalette(new ColorRgba32(4, 5, 6, 255)));

        _tree.ReplaceNodes([(paletteNode, "/Pal")], () =>
        {
            _tree.Root.DetachChildNode("Pal");
            _tree.Root.AttachChildNode(replacement);
        });

        Assert.Equal(3, _editors.Editors.Count);
        Assert.Same(a, _editors.Editors[0]);
        Assert.Same(c, _editors.Editors[2]);
        var editor = Assert.IsType<PaletteEditorViewModel>(_editors.Editors[1]);
        Assert.NotSame(stale, editor);
        Assert.Same(replacement.Item, editor.Resource);
        Assert.Same(editor, _editors.ActiveEditor);
    }

    [Fact]
    public async Task DependsOn_GraphicsEditor_PaletteOnlyInPaletteList()
    {
        var data = await AddDataFileAsync("rom");
        var codec = (IIndexedCodec)CodecFixture.Shared.CodecFactory.CreateCodec("GBA 4bpp")!;
        codec.Palette = _palette;
        var arranger = new ScatteredArranger("User", PixelColorType.Indexed, ElementLayout.Tiled, 1, 1, codec.Width, codec.Height);
        arranger.SetElement(new ArrangerElement(0, 0, (DataSource)data.Item, BitAddress.Zero, codec), 0, 0);
        Assert.True((await _projectService.AddResourceAsync(_tree.Root, arranger)).HasSucceeded);
        await _editors.ActivateEditor(arranger);
        var editor = Assert.IsType<GraphicsEditorViewModel>(_editors.ActiveEditor);
        var listed = ArrangerTestFactory.CreatePalette(new ColorRgba32(1, 2, 3, 255));
        Assert.False(editor.DependsOn(Resources(listed)));

        editor.SelectPalette(listed);

        Assert.True(editor.DependsOn(Resources(listed)));
        Assert.True(editor.DependsOn(Resources(data.Item)));
        Assert.False(editor.EditsAny(Resources(listed)));
        Assert.Same(editor, Assert.Single(_editors.FindAffected(Resources(listed))));
    }

    [Fact]
    public async Task ResolveUnsavedChanges_CancelAtSecondOfThree_StopsAndKeepsAllOpen()
    {
        var a = await AddEditorAsync("A", isModified: true);
        var b = await AddEditorAsync("B", isModified: true);
        var c = await AddEditorAsync("C", isModified: true);
        _interactions.Answers.Enqueue(PromptResult.Accept);
        _interactions.Answers.Enqueue(PromptResult.Cancel);

        var actions = await _editors.ResolveUnsavedChangesAsync([a, b, c], UnsavedChangesPrompt.Closing());

        Assert.Equal([UserSaveAction.Save, UserSaveAction.Cancel], actions);
        Assert.Equal(["'A' has been modified and will be closed. Save changes?", "'B' has been modified and will be closed. Save changes?"], _interactions.Messages);
        Assert.Equal((1, 0, 0), (a.SaveCount, b.SaveCount, c.SaveCount));
        Assert.True(b.IsModified && c.IsModified);
        Assert.Equal([a, b, c], _editors.Editors);
    }

    [Fact]
    public async Task ResolveUnsavedChanges_DiscardOnly_DiscardsAfterAllAccepted()
    {
        var a = await AddEditorAsync("A", isModified: true);
        var b = await AddEditorAsync("B", isModified: true);
        _interactions.Answers.Enqueue(PromptResult.Accept);
        _interactions.Answers.Enqueue(PromptResult.Cancel);

        var cancelled = await _editors.ResolveUnsavedChangesAsync([a, b], UnsavedChangesPrompt.DiscardForDeletion);

        Assert.Equal([UserSaveAction.Discard, UserSaveAction.Cancel], cancelled);
        Assert.True(a.IsModified && b.IsModified);
        Assert.Equal("'A' has unsaved changes that use resources being deleted. These changes will be discarded.", _interactions.Messages[0]);

        _interactions.Answers.Enqueue(PromptResult.Accept);
        _interactions.Answers.Enqueue(PromptResult.Accept);

        var accepted = await _editors.ResolveUnsavedChangesAsync([a, b], UnsavedChangesPrompt.DiscardForDeletion);

        Assert.Equal([UserSaveAction.Discard, UserSaveAction.Discard], accepted);
        Assert.False(a.IsModified || b.IsModified);
        Assert.Equal((0, 0), (a.SaveCount, b.SaveCount));
    }

    private static HashSet<IProjectResource> Resources(params IProjectResource[] resources) => resources.ToHashSet<IProjectResource>(ReferenceEqualityComparer.Instance);

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
    }
}
