using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Services;

public sealed class ProjectServiceTests : IAsyncLifetime
{
    private readonly string _directory;
    private readonly Palette _palette = ArrangerTestFactory.CreatePalette(new ColorRgba32(0, 0, 0, 255));
    private readonly ColorFactory _colorFactory = new();
    private readonly ProjectService _service;
    private ProjectTree _tree = null!;
    private readonly List<ProjectTreeChange> _changes = [];

    public ProjectServiceTests()
    {
        _directory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "rom.bin"), new byte[64]);

        _service = CreateService();
    }

    private ProjectService CreateService()
    {
        var serializerFactory = new XmlProjectSerializerFactory(Path.Combine(AppContext.BaseDirectory, "_schemas", "ResourceSchema.xsd"),
            CodecFixture.Shared.CodecFactory, _colorFactory, [_palette]);

        return new ProjectService(serializerFactory);
    }

    public async Task InitializeAsync()
    {
        _tree = (await _service.CreateNewProjectAsync(Path.Combine(_directory, "project.xml"))).AsSuccess.Result;
        _tree.Changed += (_, change) => _changes.Add(change);
    }

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

    private async Task<ResourceNode> AddDataFileAsync(ResourceNode parent, string name, string fileName = "rom.bin") =>
        (await _service.AddResourceAsync(parent, new FileDataSource(name, Path.Combine(_directory, fileName)))).AsSuccess.Result;

    private async Task<ResourceNode> AddFolderAsync(ResourceNode parent, string name) =>
        (await _service.CreateNewFolderAsync(parent, name)).AsSuccess.Result;

    private async Task<ResourceNode> AddPaletteAsync(ResourceNode parent, string name, DataSource dataSource, ColorRgba32 color)
    {
        var palette = new Palette(name, _colorFactory, ColorModel.Rgba32, [new ProjectNativeColorSource(color)], false,
            PaletteStorageSource.ProjectXml, dataSource);
        return (await _service.AddResourceAsync(parent, palette)).AsSuccess.Result;
    }

    private string PathOf(params string[] parts) => Path.Combine([_directory, .. parts]);

    private static void AssertMatchesTree(ResourceNodeViewModel vm, ResourceNode node)
    {
        Assert.Same(node, vm.Node);
        Assert.Equal(node.Name, vm.Name);
        Assert.Equal(node.ChildNodes.Count(), vm.Children.Count);
        Assert.Equal(vm.Children.Order(ResourceNodeComparer.Instance), vm.Children);

        foreach (var child in vm.Children)
        {
            Assert.Same(vm, child.ParentModel);
            Assert.Same(node, child.Node.Parent);
            AssertMatchesTree(child, child.Node);
        }
    }

    [Fact]
    public async Task RenameFolder_MovesDirectoryAndChildResource()
    {
        var folder = await AddFolderAsync(_tree.Root, "Folder");
        await AddDataFileAsync(folder, "data");

        var result = await _service.RenameResourceAsync(folder, "Renamed");

        Assert.True(result.HasSucceeded);
        Assert.False(Directory.Exists(PathOf("Folder")));
        Assert.True(File.Exists(PathOf("Renamed", "data.xml")));
        Assert.Equal(PathOf("Renamed"), folder.DiskLocation);
    }

    [Fact]
    public async Task RenameFile_LeavesOnlyNewResourceFile()
    {
        var node = await AddDataFileAsync(_tree.Root, "data");

        var result = await _service.RenameResourceAsync(node, "renamed");

        Assert.True(result.HasSucceeded);
        Assert.False(File.Exists(PathOf("data.xml")));
        Assert.True(File.Exists(PathOf("renamed.xml")));
    }

    [Fact]
    public async Task RenameFile_CaseOnly_KeepsResourceFile()
    {
        var node = await AddDataFileAsync(_tree.Root, "data");

        var result = await _service.RenameResourceAsync(node, "DATA");

        Assert.True(result.HasSucceeded);
        var xmlFiles = Directory.GetFiles(_directory, "*.xml").Select(Path.GetFileName);
        Assert.Contains("DATA.xml", xmlFiles);
        Assert.DoesNotContain("data.xml", xmlFiles);
    }

    [Fact]
    public async Task RenameFolder_UpdatesNestedFolderLocations()
    {
        var outer = await AddFolderAsync(_tree.Root, "Outer");
        var inner = await AddFolderAsync(outer, "Inner");

        var result = await _service.RenameResourceAsync(outer, "Renamed");

        Assert.True(result.HasSucceeded);
        Assert.Equal(PathOf("Renamed", "Inner"), inner.DiskLocation);
        Assert.True((await _service.RenameResourceAsync(inner, "Inner2")).HasSucceeded);
        Assert.True(Directory.Exists(PathOf("Renamed", "Inner2")));
    }

    [Fact]
    public async Task MoveNode_DestinationFileExists_FailsAndLeavesBothFiles()
    {
        var node = await AddDataFileAsync(_tree.Root, "data");
        var destination = await AddFolderAsync(_tree.Root, "Dest");
        var original = File.ReadAllText(PathOf("data.xml"));
        File.WriteAllText(PathOf("Dest", "data.xml"), "stray");

        var result = await _service.MoveNodeAsync(node, destination);

        Assert.True(result.HasFailed);
        Assert.Equal(original, File.ReadAllText(PathOf("data.xml")));
        Assert.Equal("stray", File.ReadAllText(PathOf("Dest", "data.xml")));
        Assert.Same(_tree.Root, node.Parent);
        Assert.Equal(PathOf("data.xml"), node.DiskLocation);
        Assert.DoesNotContain(_changes, x => x.Kind == ProjectTreeChangeKind.Moved);
    }

    [Fact]
    public async Task CreateNewProject_RaisesProjectOpened()
    {
        ProjectTree? opened = null;
        _service.ProjectOpened += (_, tree) => opened = tree;

        Directory.CreateDirectory(PathOf("Other"));

        var tree = (await _service.CreateNewProjectAsync(PathOf("Other", "other.xml"))).AsSuccess.Result;

        Assert.Same(tree, opened);
    }

    [Fact]
    public void CloseProject_RaisesProjectClosed()
    {
        ProjectTree? closed = null;
        _service.ProjectClosed += (_, tree) => closed = tree;

        _service.CloseProject(_tree);

        Assert.Same(_tree, closed);
    }

    [Fact]
    public async Task PaletteChange_RaisesServiceResourceChangedFromTree()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        var palette = (Palette)(await AddPaletteAsync(_tree.Root, "pal", (DataSource)data.Item, new ColorRgba32(1, 2, 3, 255))).Item;
        object? sender = null;
        IProjectResource? changed = null;
        _service.ResourceChanged += (s, resource) => (sender, changed) = (s, resource);

        palette.Reload();

        Assert.Same(_tree, sender);
        Assert.Same(palette, changed);
    }

    [Fact]
    public async Task ClosedProject_RaisesNoServiceEvents()
    {
        var data = (DataSource)(await AddDataFileAsync(_tree.Root, "data")).Item;
        var raised = false;
        _service.TreeChanged += (_, _) => raised = true;
        _service.ResourceChanged += (_, _) => raised = true;

        _service.CloseProject(_tree);
        data.NotifyDataWritten();
        _tree.Root.RemoveChildNode("data");

        Assert.False(raised);
    }

    [Fact]
    public async Task RenameFolder_DirectoryMoveFails_RaisesReverseRename()
    {
        var folder = await AddFolderAsync(_tree.Root, "Folder");
        Directory.CreateDirectory(PathOf("Renamed"));
        _changes.Clear();

        var result = await _service.RenameResourceAsync(folder, "Renamed");

        Assert.True(result.HasFailed);
        Assert.All(_changes, x => Assert.Equal(ProjectTreeChangeKind.Renamed, x.Kind));
        Assert.Equal(["Folder", "Renamed"], _changes.Select(x => x.OldName));
        Assert.Equal("Folder", folder.Name);
        Assert.True(_tree.TryFindResourceNode(folder.Item, out var found));
        Assert.Same(folder, found);
    }

    [Fact]
    public async Task MoveNode_RaisesSingleMoved()
    {
        var node = await AddDataFileAsync(_tree.Root, "data");
        var destination = await AddFolderAsync(_tree.Root, "Dest");
        _changes.Clear();

        var result = await _service.MoveNodeAsync(node, destination);

        Assert.True(result.HasSucceeded);
        var change = Assert.Single(_changes);
        Assert.Equal(ProjectTreeChangeKind.Moved, change.Kind);
        Assert.Same(destination, change.Parent);
        Assert.Same(_tree.Root, change.OldParent);
        Assert.Equal(PathOf("Dest", "data.xml"), node.DiskLocation);
        Assert.True(File.Exists(PathOf("Dest", "data.xml")));
        Assert.False(File.Exists(PathOf("data.xml")));
    }

    [Fact]
    public async Task MoveNode_ProjectWriteFails_RaisesReverseMove()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        await AddPaletteAsync(_tree.Root, "pal", (DataSource)data.Item, new ColorRgba32(1, 2, 3, 255));
        var destination = await AddFolderAsync(_tree.Root, "Dest");
        File.SetAttributes(PathOf("pal.xml"), FileAttributes.ReadOnly);
        _changes.Clear();

        try
        {
            var result = await _service.MoveNodeAsync(data, destination);

            Assert.True(result.HasFailed);
            Assert.Collection(_changes,
                x => Assert.Equal((ProjectTreeChangeKind.Moved, destination, _tree.Root), (x.Kind, x.Parent, x.OldParent)),
                x => Assert.Equal((ProjectTreeChangeKind.Moved, _tree.Root, destination), (x.Kind, x.Parent, x.OldParent)));
            Assert.Same(_tree.Root, data.Parent);
            Assert.Empty(Directory.EnumerateFiles(_directory, "*.bak", SearchOption.AllDirectories));
        }
        finally
        {
            File.SetAttributes(PathOf("pal.xml"), FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task DeleteFolder_WithDataFileAndItsPalette_RemovesEachNodeOnce()
    {
        var folder = await AddFolderAsync(_tree.Root, "Folder");
        var data = await AddDataFileAsync(folder, "data");
        var palette = await AddPaletteAsync(folder, "pal", (DataSource)data.Item, new ColorRgba32(1, 2, 3, 255));
        _changes.Clear();

        var plan = _service.PreviewResourceDeletion(folder).AsSuccess.Result;
        var result = await _service.ApplyResourceDeletionAsync(plan, _palette);

        Assert.True(result.HasSucceeded);
        Assert.Equal(3, plan.Changes.Count);
        Assert.All(_changes, x => Assert.Equal(ProjectTreeChangeKind.Removed, x.Kind));
        Assert.Equal(3, _changes.Count);
        Assert.Equal(new HashSet<ResourceNode> { folder, data, palette }, _changes.Select(x => x.Node).ToHashSet());
        Assert.False(_tree.ContainsResource(palette.Item));
    }

    [Fact]
    public async Task SaveProject_PaletteWithCommittedModel_WritesCommittedState()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        var committed = new ColorRgba32(0x11, 0x22, 0x33, 255);
        var node = (PaletteNode)await AddPaletteAsync(_tree.Root, "pal", (DataSource)data.Item, committed);
        var palette = (Palette)node.Item;
        ((ProjectNativeColorSource)palette.ColorSources[0]).Value = new ColorRgba32(0xAA, 0xBB, 0xCC, 255);
        node.CommittedModel = map => palette.MapToModel(map, _colorFactory, palette.ColorModel, palette.ZeroIndexTransparent,
            [new ProjectNativeColorSource(committed)]);

        var result = await _service.RenameResourceAsync(node, "renamed");

        Assert.True(result.HasSucceeded);
        var xml = File.ReadAllText(PathOf("renamed.xml"));
        Assert.Contains(_colorFactory.ToHexString(committed), xml);
        Assert.DoesNotContain(_colorFactory.ToHexString(new ColorRgba32(0xAA, 0xBB, 0xCC, 255)), xml);
    }

    [Fact]
    public async Task DeleteFolder_WithNestedFolder_RemovesBothDirectories()
    {
        var outer = await AddFolderAsync(_tree.Root, "Outer");
        var inner = await AddFolderAsync(outer, "Inner");
        await AddDataFileAsync(inner, "data");

        var plan = _service.PreviewResourceDeletion(outer).AsSuccess.Result;
        var result = await _service.ApplyResourceDeletionAsync(plan, _palette);

        Assert.True(result.HasSucceeded);
        Assert.False(Directory.Exists(PathOf("Outer", "Inner")));
        Assert.False(Directory.Exists(PathOf("Outer")));
    }

    [Fact]
    public async Task DeleteFolder_WithUnmanagedFile_KeepsDirectoryAndReportsIt()
    {
        var folder = await AddFolderAsync(_tree.Root, "Roms");
        File.WriteAllBytes(PathOf("Roms", "rom.bin"), new byte[4]);

        var plan = _service.PreviewResourceDeletion(folder).AsSuccess.Result;
        var result = await _service.ApplyResourceDeletionAsync(plan, _palette);

        Assert.True(result.HasFailed);
        Assert.Contains(PathOf("Roms"), result.AsError.Reason);
        Assert.True(File.Exists(PathOf("Roms", "rom.bin")));
    }

    [Fact]
    public async Task ProjectNodeViewModel_ExistingTree_MatchesSorted()
    {
        await AddDataFileAsync(_tree.Root, "data");
        var folder = await AddFolderAsync(_tree.Root, "Folder");
        await AddDataFileAsync(folder, "nested");

        var vm = new ProjectNodeViewModel(_tree);

        AssertMatchesTree(vm, _tree.Root);
        Assert.IsType<FolderNodeViewModel>(vm.Children[0]);
    }

    [Fact]
    public async Task ServiceOperations_KeepViewModelTreeInSync()
    {
        var projectVm = new ProjectNodeViewModel(_tree);

        var data = await AddDataFileAsync(_tree.Root, "b-data");
        AssertMatchesTree(projectVm, _tree.Root);

        var folder = await AddFolderAsync(_tree.Root, "Folder");
        AssertMatchesTree(projectVm, _tree.Root);
        Assert.Same(folder, projectVm.Children[0].Node);

        var palette = await AddPaletteAsync(_tree.Root, "c-palette", (DataSource)data.Item, new ColorRgba32(1, 2, 3, 255));
        await AddDataFileAsync(_tree.Root, "d-data");
        AssertMatchesTree(projectVm, _tree.Root);

        Assert.True((await _service.RenameResourceAsync(data, "z-data")).HasSucceeded);
        AssertMatchesTree(projectVm, _tree.Root);
        Assert.Same(data, projectVm.Children[^1].Node);

        var dataVm = projectVm.Find(data)!;
        dataVm.IsExpanded = true;
        Assert.True((await _service.MoveNodeAsync(data, folder)).HasSucceeded);
        AssertMatchesTree(projectVm, _tree.Root);
        Assert.Same(dataVm, projectVm.Find(data));
        Assert.True(dataVm.IsExpanded);
        Assert.Same(projectVm.Find(folder), dataVm.ParentModel);

        var plan = _service.PreviewResourceDeletion(data).AsSuccess.Result;
        Assert.True((await _service.ApplyResourceDeletionAsync(plan, _palette)).HasSucceeded);
        AssertMatchesTree(projectVm, _tree.Root);
        Assert.Null(projectVm.Find(data));
        Assert.Null(projectVm.Find(palette));
    }

    private async Task<ProjectTree> ReopenAsync()
    {
        var projectFile = _tree.Root.DiskLocation!;
        _service.CloseProject(_tree);

        var result = await _service.OpenProjectFileAsync(projectFile);
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("; ", result.AsError.Reasons) : null);
        return result.AsSuccess.Result;
    }

    [Fact]
    public async Task RenameProjectRoot_WithRoot_WritesProjectFileBesideOld()
    {
        Directory.CreateDirectory(PathOf("Resources"));
        File.WriteAllText(PathOf("project.xml"), "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<project version=\"0.9\" root=\"Resources\" />");
        var tree = await ReopenAsync();

        var result = await _service.RenameResourceAsync(tree.Root, "renamed");

        Assert.True(result.HasSucceeded);
        Assert.Equal(PathOf("renamed.xml"), tree.Root.DiskLocation);
        Assert.True(File.Exists(PathOf("renamed.xml")));
        Assert.False(File.Exists(PathOf("project.xml")));
        Assert.Empty(Directory.GetFiles(PathOf("Resources"), "*.xml", SearchOption.AllDirectories));
    }

    private static T ItemOf<T>(ProjectTree tree, string name) =>
        Assert.IsType<T>(tree.Root.ChildNodes.Single(x => x.Name == name).Item);

    [Fact]
    public async Task MoveNode_NodeWithoutDiskLocation_FailsWithoutThrowing()
    {
        var node = await AddDataFileAsync(_tree.Root, "data");
        var destination = await AddFolderAsync(_tree.Root, "Dest");
        node.DiskLocation = null;

        var result = await _service.MoveNodeAsync(node, destination);

        Assert.True(result.HasFailed);
        Assert.Same(_tree.Root, node.Parent);
    }

    [Fact]
    public async Task OpenProject_MissingDataFile_LoadsWithSourceMarkedMissing()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        await AddPaletteAsync(_tree.Root, "pal", (DataSource)data.Item, new ColorRgba32(1, 2, 3, 255));
        File.Delete(PathOf("rom.bin"));

        var tree = await ReopenAsync();

        var source = ItemOf<FileDataSource>(tree, "data");
        Assert.True(source.IsMissing);
        ItemOf<Palette>(tree, "pal");
    }

    [Fact]
    public async Task Relink_CopiesFileToExpectedLocation_AndRaisesResourceChanged()
    {
        await AddDataFileAsync(_tree.Root, "data");
        var replacement = Path.Combine(_directory, "replacement.bin");
        File.Move(PathOf("rom.bin"), replacement);

        var tree = await ReopenAsync();
        var source = ItemOf<FileDataSource>(tree, "data");
        var changed = new List<IProjectResource>();
        _service.ResourceChanged += (_, resource) => changed.Add(resource);
        Assert.ThrowsAny<IOException>(() => source.Read(BitAddress.Zero, 8));

        var result = await _service.RelinkDataFileAsync(source, replacement);

        Assert.True(result.HasSucceeded);
        Assert.False(source.IsMissing);
        Assert.True(File.Exists(replacement));
        Assert.Equal(64, new FileInfo(PathOf("rom.bin")).Length);
        Assert.Contains(source, changed);
        Assert.Single(source.Read(BitAddress.Zero, 8));
        source.Dispose();
    }

    [Fact]
    public async Task Relink_SourceNotMissing_Fails()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");

        var result = await _service.RelinkDataFileAsync((FileDataSource)data.Item, PathOf("rom.bin"));

        Assert.True(result.HasFailed);
    }

    private ProjectTree OpenStandalone(string name = "standalone.bin")
    {
        var path = PathOf(name);
        File.WriteAllBytes(path, new byte[32]);
        return _service.OpenDataFile(path).AsSuccess.Result;
    }

    private static void AssertStandaloneFailure(string reason, ProjectTree tree) =>
        Assert.Equal($"'{tree.Name}' is a standalone file, not a project", reason);

    [Fact]
    public void OpenDataFile_RaisesProjectOpened_AndTreeContainsSource()
    {
        var opened = new List<ProjectTree>();
        _service.ProjectOpened += (_, tree) => opened.Add(tree);

        var tree = OpenStandalone();

        Assert.Same(tree, Assert.Single(opened));
        Assert.True(tree.IsStandaloneFile);
        Assert.Same(tree, _service.FindContainingProject(tree.Root.Item));
    }

    [Fact]
    public void OpenDataFile_SamePathTwice_ReturnsExistingTree()
    {
        var tree = OpenStandalone();
        var opened = new List<ProjectTree>();
        _service.ProjectOpened += (_, t) => opened.Add(t);

        var again = _service.OpenDataFile(PathOf("standalone.bin"));
        var differentCase = _service.OpenDataFile(PathOf("STANDALONE.BIN"));

        Assert.Same(tree, again.AsSuccess.Result);
        Assert.Same(tree, differentCase.AsSuccess.Result);
        Assert.Empty(opened);
    }

    [Fact]
    public void OpenDataFile_MissingFile_Fails()
    {
        var result = _service.OpenDataFile(PathOf("missing.bin"));

        Assert.True(result.HasFailed);
    }

    [Fact]
    public void CloseProject_StandaloneFile_RaisesProjectClosedAndReleasesFile()
    {
        var tree = OpenStandalone();
        var source = (DataSource)tree.Root.Item;
        source.Read(BitAddress.Zero, 8);
        var closed = new List<ProjectTree>();
        _service.ProjectClosed += (_, t) => closed.Add(t);

        _service.CloseProject(tree);

        Assert.Same(tree, Assert.Single(closed));
        using var stream = File.Open(PathOf("standalone.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public async Task ProjectOnlyOperations_StandaloneFile_FailWithoutWriting()
    {
        var tree = OpenStandalone();
        var root = tree.Root;
        var filesBefore = Directory.GetFileSystemEntries(_directory, "*", SearchOption.AllDirectories).Order().ToList();

        var add = await _service.AddResourceAsync(root, new MemoryDataSource("mem", 16));
        var folder = await _service.CreateNewFolderAsync(root, "Folder");
        var rename = await _service.RenameResourceAsync(root, "renamed.bin");
        var canMove = _service.CanMoveNode(root, root);
        var move = await _service.MoveNodeAsync(root, root);
        var preview = _service.PreviewResourceDeletion(root);
        var saveAs = await _service.SaveProjectAsAsync(tree, PathOf("saved.xml"));

        Assert.True(add.HasFailed);
        Assert.True(folder.HasFailed);
        Assert.True(canMove.HasFailed);
        Assert.True(move.HasFailed);
        AssertStandaloneFailure(rename.AsError.Reason, tree);
        AssertStandaloneFailure(preview.AsError.Reason, tree);
        AssertStandaloneFailure(saveAs.AsError.Reason, tree);
        Assert.Equal("standalone.bin", root.Name);
        Assert.Equal(filesBefore, Directory.GetFileSystemEntries(_directory, "*", SearchOption.AllDirectories).Order());
    }

    [Fact]
    public async Task SaveProjectAsync_StandaloneFile_SucceedsWithoutWriting()
    {
        var tree = OpenStandalone();
        var filesBefore = Directory.GetFileSystemEntries(_directory, "*", SearchOption.AllDirectories).Order().ToList();

        var result = await _service.SaveProjectAsync(tree);

        Assert.True(result.HasSucceeded);
        Assert.Equal(filesBefore, Directory.GetFileSystemEntries(_directory, "*", SearchOption.AllDirectories).Order());
    }

    [Fact]
    public void StandaloneFileNodeViewModel_FindsOnlyItsRoot()
    {
        var tree = OpenStandalone();
        var vm = new StandaloneFileNodeViewModel(tree.Root);

        Assert.Same(vm, vm.Find(tree.Root));
        Assert.Null(vm.Find(_tree.Root));
        Assert.Empty(vm.Children);
        Assert.Null(vm.ParentModel);
    }

    private List<string> ListEntries() =>
        Directory.GetFileSystemEntries(_directory, "*", SearchOption.AllDirectories).Order().ToList();

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("con")]
    [InlineData(" a")]
    public async Task AddResource_InvalidName_FailsWithoutWriting(string name)
    {
        var entriesBefore = ListEntries();

        var result = await _service.AddResourceAsync(_tree.Root, new FileDataSource(name, PathOf("rom.bin")));

        Assert.True(result.HasFailed);
        Assert.Equal(entriesBefore, ListEntries());
        Assert.Empty(_tree.Root.ChildNodes);
    }

    [Fact]
    public async Task AddResource_CaseVariantOfSibling_FailsAndKeepsSiblingFile()
    {
        await AddDataFileAsync(_tree.Root, "data");
        var original = File.ReadAllBytes(PathOf("data.xml"));

        var result = await _service.AddResourceAsync(_tree.Root, new ScatteredArranger("Data", PixelColorType.Indexed, ElementLayout.Tiled, 1, 1, 8, 8));

        Assert.True(result.HasFailed);
        Assert.Contains("'data'", result.AsError.Reason);
        Assert.Equal(original, File.ReadAllBytes(PathOf("data.xml")));
        Assert.Single(_tree.Root.ChildNodes);
    }

    [Fact]
    public async Task AddResource_RootResourceNamedLikeProject_FailsAndKeepsProjectFile()
    {
        var original = File.ReadAllBytes(PathOf("project.xml"));

        var result = await _service.AddResourceAsync(_tree.Root, new FileDataSource("Project", PathOf("rom.bin")));

        Assert.True(result.HasFailed);
        Assert.Equal(original, File.ReadAllBytes(PathOf("project.xml")));
        Assert.Empty(_tree.Root.ChildNodes);
    }

    [Fact]
    public async Task CreateNewFolder_ExistingName_Fails()
    {
        await AddFolderAsync(_tree.Root, "New Folder");

        var result = await _service.CreateNewFolderAsync(_tree.Root, "new folder");

        Assert.True(result.HasFailed);
        Assert.Single(_tree.Root.ChildNodes);
    }

    [Theory]
    [InlineData("Bad?")]
    [InlineData("x.xml")]
    public async Task CreateNewFolder_InvalidName_CreatesNoDirectory(string name)
    {
        var entriesBefore = ListEntries();

        var result = await _service.CreateNewFolderAsync(_tree.Root, name);

        Assert.True(result.HasFailed);
        Assert.Equal(entriesBefore, ListEntries());
    }

    [Fact]
    public async Task RenameResource_SameName_SucceedsWithoutRenamed()
    {
        var node = await AddDataFileAsync(_tree.Root, "data");
        var entriesBefore = ListEntries();
        var dataWrite = File.GetLastWriteTimeUtc(PathOf("data.xml"));
        var projectWrite = File.GetLastWriteTimeUtc(PathOf("project.xml"));
        _changes.Clear();

        var result = await _service.RenameResourceAsync(node, "data");

        Assert.True(result.HasSucceeded);
        Assert.Empty(_changes);
        Assert.Equal(entriesBefore, ListEntries());
        Assert.Equal(dataWrite, File.GetLastWriteTimeUtc(PathOf("data.xml")));
        Assert.Equal(projectWrite, File.GetLastWriteTimeUtc(PathOf("project.xml")));
    }

    [Fact]
    public async Task RenameResource_CaseVariantOfSibling_Fails()
    {
        await AddDataFileAsync(_tree.Root, "data");
        var other = await AddDataFileAsync(_tree.Root, "other");

        var result = await _service.RenameResourceAsync(other, "DATA");

        Assert.True(result.HasFailed);
        Assert.Equal("other", other.Name);
        Assert.True(File.Exists(PathOf("other.xml")));
    }

    [Fact]
    public async Task RenameProject_ToRootResourceName_Fails()
    {
        await AddDataFileAsync(_tree.Root, "data");
        await AddFolderAsync(_tree.Root, "Folder");

        var result = await _service.RenameResourceAsync(_tree.Root, "Data");

        Assert.True(result.HasFailed);
        Assert.Equal("project", _tree.Name);
        Assert.True(File.Exists(PathOf("project.xml")));
        Assert.True(_service.CanRenameResource(_tree.Root, "folder").HasSucceeded);
    }

    [Fact]
    public async Task MoveNode_TargetHasCaseVariant_Fails()
    {
        var node = await AddDataFileAsync(_tree.Root, "data");
        var destination = await AddFolderAsync(_tree.Root, "Dest");
        await AddDataFileAsync(destination, "Data");

        var canMove = _service.CanMoveNode(node, destination);
        var result = await _service.MoveNodeAsync(node, destination);

        Assert.True(canMove.HasFailed);
        Assert.True(result.HasFailed);
        Assert.Same(_tree.Root, node.Parent);
        Assert.True(File.Exists(PathOf("data.xml")));
    }

    [Fact]
    public async Task CreateNewProjectWithExistingFile_FileInOpenProject_Fails()
    {
        await AddDataFileAsync(_tree.Root, "data");

        var result = await _service.CreateNewProjectWithExistingFileAsync(PathOf("romProject.xml"), PathOf("ROM.bin"));

        Assert.True(result.HasFailed);
        Assert.Equal("'ROM.bin' is already in project 'project'", result.AsError.Reason);
        Assert.False(File.Exists(PathOf("romProject.xml")));
    }

    [Theory]
    [InlineData("con.xml")]
    [InlineData("a .xml")]
    public async Task CreateNewProject_InvalidName_FailsWithoutWriting(string fileName)
    {
        var entriesBefore = ListEntries();

        var result = await _service.CreateNewProjectAsync(PathOf(fileName));

        Assert.True(result.HasFailed);
        Assert.Equal(entriesBefore, ListEntries());
    }

    [Fact]
    public async Task CanRenameResource_MatchesRenameFailure()
    {
        await AddDataFileAsync(_tree.Root, "data");
        var other = await AddDataFileAsync(_tree.Root, "other");

        foreach (var name in new[] { "", "a/b", "Data", "project", "lpt1" })
        {
            var canRename = _service.CanRenameResource(other, name);
            var rename = await _service.RenameResourceAsync(other, name);

            Assert.True(canRename.HasFailed, name);
            Assert.Equal(canRename.AsError.Reason, rename.AsError.Reason);
        }

        Assert.True(_service.CanRenameResource(other, "other").HasSucceeded);
        Assert.True(_service.CanRenameResource(other, "Other").HasSucceeded);
    }

    [Fact]
    public async Task OpenProject_NameBreakingRule_LoadsAndMoves()
    {
        await AddDataFileAsync(_tree.Root, "data");
        var longName = new string('n', 120);
        File.Move(PathOf("data.xml"), PathOf($"{longName}.xml"));
        Directory.CreateDirectory(PathOf(" Lead"));

        var tree = await ReopenAsync();

        var node = tree.Root.ChildNodes.Single(x => x.Name == longName);
        var folder = tree.Root.ChildNodes.Single(x => x.Name == " Lead");
        Assert.True((await _service.RenameResourceAsync(node, longName)).HasSucceeded);
        Assert.True((await _service.MoveNodeAsync(node, folder)).HasSucceeded);
        Assert.Same(folder, node.Parent);
        Assert.True(File.Exists(PathOf(" Lead", $"{longName}.xml")));
    }

    private static void AssertNoTransactionFiles(string directory)
    {
        Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(directory, "*.bak", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(directory, "_transaction.json", SearchOption.AllDirectories));
    }

    private static Palette PaletteOf(ResourceNode node) => (Palette)node.Item;
    private static DataSource SourceOf(ResourceNode node) => (DataSource)node.Item;

    private static IIndexedCodec IndexedCodecAt(Arranger arranger, int x) =>
        (IIndexedCodec)arranger.GetElement(x, 0)!.Value.Codec;

    private async Task<ArrangerNode> AddArrangerAsync(ResourceNode parent, string name, params (ResourceNode Source, ResourceNode Palette)[] elements)
    {
        var arranger = new ScatteredArranger(name, PixelColorType.Indexed, ElementLayout.Tiled, elements.Length, 1, 8, 8);

        for (int x = 0; x < elements.Length; x++)
        {
            var codec = (IIndexedCodec)CodecFixture.Shared.CodecFactory.CreateCodec("GBA 4bpp")!;
            codec.Palette = PaletteOf(elements[x].Palette);
            arranger.SetElement(new ArrangerElement(x * 8, 0, SourceOf(elements[x].Source), BitAddress.Zero, codec), x, 0);
        }

        return (ArrangerNode)(await _service.AddResourceAsync(parent, arranger)).AsSuccess.Result;
    }

    private async Task<MagitekResult> DeleteAsync(ResourceNode node)
    {
        var plan = _service.PreviewResourceDeletion(node).AsSuccess.Result;
        return await _service.ApplyResourceDeletionAsync(plan, _palette);
    }

    [Fact]
    public async Task ConcurrentSaveAndSaveResource_BothSucceed_NoLeftoverFiles()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        var pal = await AddPaletteAsync(_tree.Root, "pal", SourceOf(data), new ColorRgba32(1, 1, 1, 255));
        var other = await AddPaletteAsync(_tree.Root, "other", SourceOf(data), new ColorRgba32(1, 1, 1, 255));
        var palColor = new ColorRgba32(0x12, 0x34, 0x56, 255);
        var otherColor = new ColorRgba32(0x65, 0x43, 0x21, 255);
        ((ProjectNativeColorSource)PaletteOf(pal).ColorSources[0]).Value = palColor;
        ((ProjectNativeColorSource)PaletteOf(other).ColorSources[0]).Value = otherColor;

        var save = _service.SaveProjectAsync(_tree);
        var saveResource = _service.SaveResourceAsync(_tree, other, true);
        var results = await Task.WhenAll(save, saveResource);

        Assert.All(results, x => Assert.True(x.HasSucceeded));
        Assert.Contains(_colorFactory.ToHexString(palColor), File.ReadAllText(PathOf("pal.xml")));
        Assert.Contains(_colorFactory.ToHexString(otherColor), File.ReadAllText(PathOf("other.xml")));
        AssertNoTransactionFiles(_directory);
    }

    [Fact]
    public async Task CreateNewProject_ExistingFile_FailsAndKeepsFile()
    {
        Directory.CreateDirectory(PathOf("New"));
        File.WriteAllText(PathOf("New", "existing.xml"), "keep");

        var result = await _service.CreateNewProjectAsync(PathOf("New", "existing.xml"));

        Assert.True(result.HasFailed);
        Assert.Contains("already exists", result.AsError.Reason);
        Assert.Equal("keep", File.ReadAllText(PathOf("New", "existing.xml")));
    }

    [Fact]
    public async Task CreateNewProject_MissingDirectory_Fails()
    {
        var result = await _service.CreateNewProjectAsync(PathOf("Missing", "new.xml"));

        Assert.True(result.HasFailed);
        Assert.False(Directory.Exists(PathOf("Missing")));
    }

    [Fact]
    public async Task CreateNewProject_DirectoryHoldsXml_Fails()
    {
        Directory.CreateDirectory(PathOf("Holder", "Nested"));
        File.WriteAllText(PathOf("Holder", "Nested", "stray.xml"), "<stray />");

        var result = await _service.CreateNewProjectAsync(PathOf("Holder", "new.xml"));

        Assert.True(result.HasFailed);
        Assert.False(File.Exists(PathOf("Holder", "new.xml")));
    }

    [Fact]
    public async Task CreateNewProject_PathOfOpenProject_Fails()
    {
        File.Delete(PathOf("project.xml"));

        var result = await _service.CreateNewProjectAsync(PathOf("PROJECT.xml"));

        Assert.True(result.HasFailed);
        Assert.Contains("already open", result.AsError.Reason);
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.xml"));
    }

    [Fact]
    public async Task CreateNewProjectWithExistingFile_ProjectFileExists_Fails()
    {
        File.WriteAllText(PathOf("romProject.xml"), "keep");

        var result = await _service.CreateNewProjectWithExistingFileAsync(PathOf("romProject.xml"), PathOf("rom.bin"));

        Assert.True(result.HasFailed);
        Assert.Equal("keep", File.ReadAllText(PathOf("romProject.xml")));
    }

    [Fact]
    public async Task CreateNewProjectWithExistingFile_DataFileMissing_Fails()
    {
        var result = await _service.CreateNewProjectWithExistingFileAsync(PathOf("romProject.xml"), PathOf("missing.bin"));

        Assert.True(result.HasFailed);
        Assert.False(File.Exists(PathOf("romProject.xml")));
    }

    [Fact]
    public async Task CreateNewProjectWithExistingFile_Succeeds_RaisesProjectOpenedAndLoads()
    {
        Directory.CreateDirectory(PathOf("Fresh"));
        File.WriteAllBytes(PathOf("Fresh", "game.bin"), new byte[16]);
        ProjectTree? opened = null;
        _service.ProjectOpened += (_, tree) => opened = tree;

        var result = await _service.CreateNewProjectWithExistingFileAsync(PathOf("Fresh", "gameProject.xml"), PathOf("Fresh", "game.bin"));

        Assert.True(result.HasSucceeded);
        Assert.Same(result.AsSuccess.Result, opened);
        _service.CloseProject(opened!);

        var reopened = await _service.OpenProjectFileAsync(PathOf("Fresh", "gameProject.xml"));
        var source = ItemOf<FileDataSource>(reopened.AsSuccess.Result, "game");
        Assert.Equal(PathOf("Fresh", "game.bin"), Path.GetFullPath(source.FileLocation));
        AssertNoTransactionFiles(PathOf("Fresh"));
    }

    [Fact]
    public async Task AddResource_WritesFileWithoutLeftovers()
    {
        var node = await AddDataFileAsync(_tree.Root, "data");

        Assert.Equal(PathOf("data.xml"), node.DiskLocation);
        Assert.NotNull(node.Model);
        Assert.Contains("datafile", File.ReadAllText(PathOf("data.xml")));
        AssertNoTransactionFiles(_directory);
    }

    [Fact]
    public async Task DeleteDataFile_ArrangerOnTwoDataFiles_SurvivesWithOnlyOtherElements()
    {
        File.WriteAllBytes(PathOf("rom2.bin"), new byte[64]);
        var dataA = await AddDataFileAsync(_tree.Root, "a");
        var dataB = await AddDataFileAsync(_tree.Root, "b", "rom2.bin");
        var pal = await AddPaletteAsync(_tree.Root, "pal", SourceOf(dataB), new ColorRgba32(1, 2, 3, 255));
        var arrangerNode = await AddArrangerAsync(_tree.Root, "arr", (dataA, pal), (dataB, pal));
        var arranger = (Arranger)arrangerNode.Item;

        var plan = _service.PreviewResourceDeletion(dataA).AsSuccess.Result;
        var change = plan.Changes.Single(x => ReferenceEquals(x.ResourceNode, arrangerNode));
        var result = await _service.ApplyResourceDeletionAsync(plan, _palette);

        Assert.Equal((false, true, false), (change.Removed, change.LostElement, change.LostPalette));
        Assert.True(result.HasSucceeded);
        Assert.True(_tree.ContainsResource(arranger));
        Assert.Null(arranger.GetElement(0, 0));
        Assert.Same(dataB.Item, arranger.GetElement(1, 0)?.Source);
        Assert.Single(XDocument.Load(PathOf("arr.xml")).Root!.Elements("element"));
    }

    [Fact]
    public async Task DeleteDataFile_ArrangerOnlyOnIt_IsRemoved()
    {
        File.WriteAllBytes(PathOf("rom2.bin"), new byte[64]);
        var dataA = await AddDataFileAsync(_tree.Root, "a");
        var dataB = await AddDataFileAsync(_tree.Root, "b", "rom2.bin");
        var pal = await AddPaletteAsync(_tree.Root, "pal", SourceOf(dataB), new ColorRgba32(1, 2, 3, 255));
        var arrangerNode = await AddArrangerAsync(_tree.Root, "arr", (dataA, pal), (dataA, pal));

        var plan = _service.PreviewResourceDeletion(dataA).AsSuccess.Result;
        var result = await _service.ApplyResourceDeletionAsync(plan, _palette);

        Assert.True(plan.Changes.Single(x => ReferenceEquals(x.ResourceNode, arrangerNode)).Removed);
        Assert.True(result.HasSucceeded);
        Assert.False(_tree.ContainsResource(arrangerNode.Item));
        Assert.False(File.Exists(PathOf("arr.xml")));
        Assert.True(_tree.ContainsResource(pal.Item));
    }

    [Fact]
    public async Task DeletePalette_OnlyElementsOnItGetFallback()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        var removedPal = await AddPaletteAsync(_tree.Root, "pal1", SourceOf(data), new ColorRgba32(1, 2, 3, 255));
        var keptPal = await AddPaletteAsync(_tree.Root, "pal2", SourceOf(data), new ColorRgba32(4, 5, 6, 255));
        var arrangerNode = await AddArrangerAsync(_tree.Root, "arr", (data, removedPal), (data, keptPal));
        var arranger = (Arranger)arrangerNode.Item;

        var plan = _service.PreviewResourceDeletion(removedPal).AsSuccess.Result;
        var change = plan.Changes.Single(x => ReferenceEquals(x.ResourceNode, arrangerNode));
        var result = await _service.ApplyResourceDeletionAsync(plan, _palette);

        Assert.Equal((false, false, true), (change.Removed, change.LostElement, change.LostPalette));
        Assert.True(result.HasSucceeded);
        Assert.Same(_palette, IndexedCodecAt(arranger, 0).Palette);
        Assert.Same(keptPal.Item, IndexedCodecAt(arranger, 1).Palette);
    }

    [Fact]
    public async Task DeletePalette_ArrangerFileRewrittenWithFallbackKey()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        var removedPal = await AddPaletteAsync(_tree.Root, "pal1", SourceOf(data), new ColorRgba32(1, 2, 3, 255));
        var keptPal = await AddPaletteAsync(_tree.Root, "pal2", SourceOf(data), new ColorRgba32(4, 5, 6, 255));
        await AddArrangerAsync(_tree.Root, "arr", (data, removedPal), (data, keptPal));

        Assert.True((await DeleteAsync(removedPal)).HasSucceeded);
        var tree = await ReopenAsync();

        var arranger = ItemOf<ScatteredArranger>(tree, "arr");
        Assert.Equal(_palette.Name, IndexedCodecAt(arranger, 0).Palette.Name);
        Assert.Same(ItemOf<Palette>(tree, "pal2"), IndexedCodecAt(arranger, 1).Palette);
    }

    [Fact]
    public async Task DeleteDataFile_KeepsDataFileOnDisk()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");

        Assert.True((await DeleteAsync(data)).HasSucceeded);

        Assert.False(File.Exists(PathOf("data.xml")));
        Assert.Equal(64, new FileInfo(PathOf("rom.bin")).Length);
    }

    [Fact]
    public async Task DeleteDataFile_ReleasesFileHandle()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        SourceOf(data).Read(BitAddress.Zero, 8);

        Assert.True((await DeleteAsync(data)).HasSucceeded);

        File.Delete(PathOf("rom.bin"));
        Assert.False(File.Exists(PathOf("rom.bin")));
    }

    [Fact]
    public async Task ApplyDeletion_TransactionFails_LeavesTreeResourcesAndDiskUnchanged()
    {
        File.WriteAllBytes(PathOf("rom2.bin"), new byte[64]);
        var dataA = await AddDataFileAsync(_tree.Root, "a");
        var dataB = await AddDataFileAsync(_tree.Root, "b", "rom2.bin");
        var pal = await AddPaletteAsync(_tree.Root, "pal", SourceOf(dataB), new ColorRgba32(1, 2, 3, 255));
        var arrangerNode = await AddArrangerAsync(_tree.Root, "arr", (dataA, pal), (dataB, pal));
        var arranger = (Arranger)arrangerNode.Item;
        var modelBefore = arrangerNode.Model;
        var entriesBefore = ListEntries();
        var arrangerXml = File.ReadAllText(PathOf("arr.xml"));
        File.SetAttributes(PathOf("arr.xml"), FileAttributes.ReadOnly);
        _changes.Clear();

        try
        {
            var result = await DeleteAsync(dataA);

            Assert.True(result.HasFailed);
            Assert.Empty(_changes);
            Assert.True(_tree.ContainsResource(dataA.Item));
            Assert.Same(dataA.Item, arranger.GetElement(0, 0)?.Source);
            Assert.Same(modelBefore, arrangerNode.Model);
            Assert.Equal(arrangerXml, File.ReadAllText(PathOf("arr.xml")));
            Assert.Equal(entriesBefore, ListEntries());
        }
        finally
        {
            File.SetAttributes(PathOf("arr.xml"), FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task ApplyDeletion_ResourceFileLocked_RemovesNodeAndReportsFile()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");

        MagitekResult result;
        using (new FileStream(PathOf("data.xml"), FileMode.Open, FileAccess.Read, FileShare.None))
            result = await DeleteAsync(data);

        Assert.True(result.HasFailed);
        Assert.Contains(PathOf("data.xml"), result.AsError.Reason);
        Assert.False(_tree.ContainsResource(data.Item));
    }

    private static Dictionary<string, byte[]> ReadFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(x => x, File.ReadAllBytes);

    private async Task<(ResourceNode Data, ResourceNode Folder, ResourceNode Palette)> CreateSaveAsProjectAsync()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        var folder = await AddFolderAsync(_tree.Root, "Folder");
        var pal = await AddPaletteAsync(folder, "pal", SourceOf(data), new ColorRgba32(1, 2, 3, 255));
        await AddArrangerAsync(folder, "arr", (data, pal));
        return (data, folder, pal);
    }

    [Fact]
    public async Task SaveProjectAs_NewDirectory_ProducesProjectThatLoads()
    {
        var (_, folder, _) = await CreateSaveAsProjectAsync();
        // The journal would fail to write here if it were placed in the old directory
        Directory.CreateDirectory(PathOf("_transaction.json"));
        var oldFiles = ReadFiles(_directory);
        var newDirectory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(newDirectory);
        var newProjectFile = Path.Combine(newDirectory, "copy.xml");
        var otherService = CreateService();

        try
        {
            var result = await _service.SaveProjectAsAsync(_tree, newProjectFile);

            Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
            Assert.Equal("copy", _tree.Name);
            Assert.Equal(newProjectFile, _tree.Root.DiskLocation);
            Assert.Equal(newDirectory, ((ProjectNode)_tree.Root).BaseDirectory);
            Assert.Equal(Path.Combine(newDirectory, "Folder"), folder.DiskLocation);
            Assert.Equal(oldFiles.Keys.Order(), ReadFiles(_directory).Keys.Order());
            Assert.All(oldFiles, x => Assert.Equal(x.Value, File.ReadAllBytes(x.Key)));
            AssertNoTransactionFiles(newDirectory);

            var copy = (await otherService.OpenProjectFileAsync(newProjectFile)).AsSuccess.Result;
            var copyFolder = copy.Root.ChildNodes.Single(x => x.Name == "Folder");
            var copyPalette = (Palette)copyFolder.ChildNodes.Single(x => x.Name == "pal").Item;
            var copyArranger = (ScatteredArranger)copyFolder.ChildNodes.Single(x => x.Name == "arr").Item;
            var copySource = ItemOf<FileDataSource>(copy, "data");

            Assert.Equal(["data", "Folder"], copy.Root.ChildNodes.Select(x => x.Name).Order());
            Assert.Equal(new ColorRgba32(1, 2, 3, 255), copyPalette.GetNativeColor(0));
            Assert.Same(copySource, copyArranger.GetElement(0, 0)?.Source);
            Assert.Same(copyPalette, IndexedCodecAt(copyArranger, 0).Palette);
            Assert.Equal(PathOf("rom.bin"), Path.GetFullPath(copySource.FileLocation));
        }
        finally
        {
            otherService.CloseProjects();
            Directory.Delete(newDirectory, true);
        }
    }

    [Fact]
    public async Task SaveProjectAs_ThenSave_WritesToNewLocation()
    {
        var (_, _, pal) = await CreateSaveAsProjectAsync();
        var oldPaletteXml = File.ReadAllText(PathOf("Folder", "pal.xml"));
        var newDirectory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(newDirectory);

        try
        {
            Assert.True((await _service.SaveProjectAsAsync(_tree, Path.Combine(newDirectory, "copy.xml"))).HasSucceeded);
            var newColor = new ColorRgba32(0x77, 0x66, 0x55, 255);
            ((ProjectNativeColorSource)PaletteOf(pal).ColorSources[0]).Value = newColor;

            Assert.True((await _service.SaveProjectAsync(_tree)).HasSucceeded);
            await AddDataFileAsync(_tree.Root, "late");

            Assert.Contains(_colorFactory.ToHexString(newColor), File.ReadAllText(Path.Combine(newDirectory, "Folder", "pal.xml")));
            Assert.Equal(oldPaletteXml, File.ReadAllText(PathOf("Folder", "pal.xml")));
            Assert.True(File.Exists(Path.Combine(newDirectory, "late.xml")));
            Assert.False(File.Exists(PathOf("late.xml")));
        }
        finally
        {
            _service.CloseProjects();
            Directory.Delete(newDirectory, true);
        }
    }

    [Fact]
    public async Task SaveProjectAs_TargetDirectoryHoldsXml_FailsWithoutWriting()
    {
        await CreateSaveAsProjectAsync();
        Directory.CreateDirectory(PathOf("Target", "Nested"));
        File.WriteAllText(PathOf("Target", "Nested", "stray.xml"), "<stray />");
        var entriesBefore = ListEntries();

        var result = await _service.SaveProjectAsAsync(_tree, PathOf("Target", "copy.xml"));

        Assert.True(result.HasFailed);
        Assert.Equal(entriesBefore, ListEntries());
        Assert.Equal("project", _tree.Name);
        Assert.Equal(PathOf("project.xml"), _tree.Root.DiskLocation);
    }

    [Fact]
    public async Task SaveProjectAs_WriteFails_RestoresLocationsAndName()
    {
        var (data, folder, _) = await CreateSaveAsProjectAsync();
        var newDirectory = PathOf("Target");
        // A directory where a resource file goes makes its replace fail inside the transaction
        Directory.CreateDirectory(Path.Combine(newDirectory, "data.xml"));
        var modelBefore = _tree.Root.Model;
        _changes.Clear();

        var result = await _service.SaveProjectAsAsync(_tree, Path.Combine(newDirectory, "copy.xml"));

        Assert.True(result.HasFailed);
        Assert.Equal("project", _tree.Name);
        Assert.Equal(PathOf("project.xml"), _tree.Root.DiskLocation);
        Assert.Equal(_directory, ((ProjectNode)_tree.Root).BaseDirectory);
        Assert.Same(modelBefore, _tree.Root.Model);
        Assert.Equal(PathOf("data.xml"), data.DiskLocation);
        Assert.Equal(PathOf("Folder"), folder.DiskLocation);
        Assert.Equal(["project", "copy"], _changes.Select(x => x.OldName));
        Assert.Equal([Path.Combine(newDirectory, "data.xml")], Directory.EnumerateFileSystemEntries(newDirectory));
        Assert.True((await _service.SaveProjectAsync(_tree)).HasSucceeded);
    }

    [Fact]
    public async Task SaveResource_WritesOnlyThatResource()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        var pal = await AddPaletteAsync(_tree.Root, "pal", SourceOf(data), new ColorRgba32(1, 1, 1, 255));
        var other = await AddPaletteAsync(_tree.Root, "other", SourceOf(data), new ColorRgba32(1, 1, 1, 255));
        var otherXml = File.ReadAllText(PathOf("other.xml"));
        var newColor = new ColorRgba32(0x12, 0x34, 0x56, 255);
        ((ProjectNativeColorSource)PaletteOf(pal).ColorSources[0]).Value = newColor;
        ((ProjectNativeColorSource)PaletteOf(other).ColorSources[0]).Value = newColor;

        var result = await _service.SaveResourceAsync(_tree, pal, false);

        Assert.True(result.HasSucceeded);
        Assert.Contains(_colorFactory.ToHexString(newColor), File.ReadAllText(PathOf("pal.xml")));
        Assert.Equal(otherXml, File.ReadAllText(PathOf("other.xml")));
    }

    [Fact]
    public async Task SaveResource_NoDiskLocation_Fails()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        data.DiskLocation = null;

        var result = await _service.SaveResourceAsync(_tree, data, true);

        Assert.True(result.HasFailed);
    }

    [Fact]
    public async Task SaveAndSaveAs_WriterThrows_FailWithoutThrowing()
    {
        File.WriteAllBytes(PathOf("rom2.bin"), new byte[64]);
        var dataA = await AddDataFileAsync(_tree.Root, "a");
        var dataB = await AddDataFileAsync(_tree.Root, "b", "rom2.bin");
        var pal = await AddPaletteAsync(_tree.Root, "pal", SourceOf(dataB), new ColorRgba32(1, 2, 3, 255));
        await AddArrangerAsync(_tree.Root, "arr", (dataA, pal));
        // Detaching outside the service leaves the arranger on a source the writer cannot map
        _tree.Root.RemoveChildNode("a");
        Directory.CreateDirectory(PathOf("Target"));

        var save = await _service.SaveProjectAsync(_tree);
        var saveAs = await _service.SaveProjectAsAsync(_tree, PathOf("Target", "copy.xml"));

        Assert.True(save.HasFailed);
        Assert.True(saveAs.HasFailed);
        Assert.Equal(PathOf("project.xml"), _tree.Root.DiskLocation);
        Assert.Empty(Directory.EnumerateFileSystemEntries(PathOf("Target")));
        SourceOf(dataA).Dispose();
    }

    [Fact]
    public async Task RenameFile_ProjectWriteFails_RestoresNameAndKeepsOldFile()
    {
        var data = await AddDataFileAsync(_tree.Root, "data");
        await AddPaletteAsync(_tree.Root, "pal", SourceOf(data), new ColorRgba32(1, 2, 3, 255));
        var paletteXml = File.ReadAllText(PathOf("pal.xml"));
        File.SetAttributes(PathOf("pal.xml"), FileAttributes.ReadOnly);

        try
        {
            var result = await _service.RenameResourceAsync(data, "renamed");

            Assert.True(result.HasFailed);
            Assert.Equal("data", data.Name);
            Assert.Equal(PathOf("data.xml"), data.DiskLocation);
            Assert.True(File.Exists(PathOf("data.xml")));
            Assert.False(File.Exists(PathOf("renamed.xml")));
            Assert.Equal(paletteXml, File.ReadAllText(PathOf("pal.xml")));
            AssertNoTransactionFiles(_directory);
        }
        finally
        {
            File.SetAttributes(PathOf("pal.xml"), FileAttributes.Normal);
        }
    }
}
