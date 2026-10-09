using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Services;

public sealed class ProjectServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly Palette _palette = ArrangerTestFactory.CreatePalette(new ColorRgba32(0, 0, 0, 255));
    private readonly ColorFactory _colorFactory = new();
    private readonly ProjectService _service;
    private readonly ProjectTree _tree;
    private readonly List<ProjectTreeChange> _changes = [];

    public ProjectServiceTests()
    {
        _directory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "rom.bin"), new byte[64]);

        var serializerFactory = new XmlProjectSerializerFactory(Path.Combine(AppContext.BaseDirectory, "_schemas", "ResourceSchema.xsd"),
            CodecFixture.Shared.CodecFactory, _colorFactory, [_palette]);

        _service = new ProjectService(serializerFactory, _colorFactory);
        _tree = _service.CreateNewProject(Path.Combine(_directory, "project.xml")).AsSuccess.Result;
        _tree.Changed += (_, change) => _changes.Add(change);
    }

    public void Dispose()
    {
        _service.CloseProjects();

        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }
    }

    private ResourceNode AddDataFile(ResourceNode parent, string name) =>
        _service.AddResource(parent, new FileDataSource(name, Path.Combine(_directory, "rom.bin"))).AsSuccess.Result;

    private ResourceNode AddFolder(ResourceNode parent, string name) =>
        _service.CreateNewFolder(parent, name).AsSuccess.Result;

    private ResourceNode AddPalette(ResourceNode parent, string name, DataSource dataSource, ColorRgba32 color)
    {
        var palette = new Palette(name, _colorFactory, ColorModel.Rgba32, [new ProjectNativeColorSource(color)], false,
            PaletteStorageSource.ProjectXml, dataSource);
        return _service.AddResource(parent, palette).AsSuccess.Result;
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
        var folder = AddFolder(_tree.Root, "Folder");
        AddDataFile(folder, "data");

        var result = await _service.RenameResourceAsync(folder, "Renamed");

        Assert.True(result.HasSucceeded);
        Assert.False(Directory.Exists(PathOf("Folder")));
        Assert.True(File.Exists(PathOf("Renamed", "data.xml")));
        Assert.Equal(PathOf("Renamed"), folder.DiskLocation);
    }

    [Fact]
    public async Task RenameFile_LeavesOnlyNewResourceFile()
    {
        var node = AddDataFile(_tree.Root, "data");

        var result = await _service.RenameResourceAsync(node, "renamed");

        Assert.True(result.HasSucceeded);
        Assert.False(File.Exists(PathOf("data.xml")));
        Assert.True(File.Exists(PathOf("renamed.xml")));
    }

    [Fact]
    public async Task RenameFile_CaseOnly_KeepsResourceFile()
    {
        var node = AddDataFile(_tree.Root, "data");

        var result = await _service.RenameResourceAsync(node, "DATA");

        Assert.True(result.HasSucceeded);
        var xmlFiles = Directory.GetFiles(_directory, "*.xml").Select(Path.GetFileName);
        Assert.Contains("DATA.xml", xmlFiles);
        Assert.DoesNotContain("data.xml", xmlFiles);
    }

    [Fact]
    public async Task RenameFolder_UpdatesNestedFolderLocations()
    {
        var outer = AddFolder(_tree.Root, "Outer");
        var inner = AddFolder(outer, "Inner");

        var result = await _service.RenameResourceAsync(outer, "Renamed");

        Assert.True(result.HasSucceeded);
        Assert.Equal(PathOf("Renamed", "Inner"), inner.DiskLocation);
        Assert.True((await _service.RenameResourceAsync(inner, "Inner2")).HasSucceeded);
        Assert.True(Directory.Exists(PathOf("Renamed", "Inner2")));
    }

    [Fact]
    public async Task MoveNode_DestinationFileExists_FailsAndLeavesBothFiles()
    {
        var node = AddDataFile(_tree.Root, "data");
        var destination = AddFolder(_tree.Root, "Dest");
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
    public void CreateNewProject_RaisesProjectOpened()
    {
        ProjectTree? opened = null;
        _service.ProjectOpened += (_, tree) => opened = tree;

        var tree = _service.CreateNewProject(PathOf("other.xml")).AsSuccess.Result;

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
    public void PaletteChange_RaisesServiceResourceChangedFromTree()
    {
        var data = AddDataFile(_tree.Root, "data");
        var palette = (Palette)AddPalette(_tree.Root, "pal", (DataSource)data.Item, new ColorRgba32(1, 2, 3, 255)).Item;
        object? sender = null;
        IProjectResource? changed = null;
        _service.ResourceChanged += (s, resource) => (sender, changed) = (s, resource);

        palette.Reload();

        Assert.Same(_tree, sender);
        Assert.Same(palette, changed);
    }

    [Fact]
    public void ClosedProject_RaisesNoServiceEvents()
    {
        var data = (DataSource)AddDataFile(_tree.Root, "data").Item;
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
        var folder = AddFolder(_tree.Root, "Folder");
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
        var node = AddDataFile(_tree.Root, "data");
        var destination = AddFolder(_tree.Root, "Dest");
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
        var data = AddDataFile(_tree.Root, "data");
        AddPalette(_tree.Root, "pal", (DataSource)data.Item, new ColorRgba32(1, 2, 3, 255));
        var destination = AddFolder(_tree.Root, "Dest");
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
        }
        finally
        {
            // The transaction's backup copies the read-only attribute too
            foreach (var file in Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    [Fact]
    public void DeleteFolder_WithDataFileAndItsPalette_RemovesEachNodeOnce()
    {
        var folder = AddFolder(_tree.Root, "Folder");
        var data = AddDataFile(folder, "data");
        var palette = AddPalette(folder, "pal", (DataSource)data.Item, new ColorRgba32(1, 2, 3, 255));
        _changes.Clear();

        var plan = _service.PreviewResourceDeletion(folder);
        var result = _service.ApplyResourceDeletion(plan, _palette);

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
        var data = AddDataFile(_tree.Root, "data");
        var committed = new ColorRgba32(0x11, 0x22, 0x33, 255);
        var node = (PaletteNode)AddPalette(_tree.Root, "pal", (DataSource)data.Item, committed);
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
    public void DeleteFolder_WithNestedFolder_RemovesBothDirectories()
    {
        var outer = AddFolder(_tree.Root, "Outer");
        var inner = AddFolder(outer, "Inner");
        AddDataFile(inner, "data");

        var plan = _service.PreviewResourceDeletion(outer);
        var result = _service.ApplyResourceDeletion(plan, _palette);

        Assert.True(result.HasSucceeded);
        Assert.False(Directory.Exists(PathOf("Outer", "Inner")));
        Assert.False(Directory.Exists(PathOf("Outer")));
    }

    [Fact]
    public void DeleteFolder_WithUnmanagedFile_KeepsDirectoryAndReportsIt()
    {
        var folder = AddFolder(_tree.Root, "Roms");
        File.WriteAllBytes(PathOf("Roms", "rom.bin"), new byte[4]);

        var plan = _service.PreviewResourceDeletion(folder);
        var result = _service.ApplyResourceDeletion(plan, _palette);

        Assert.True(result.HasFailed);
        Assert.Contains(PathOf("Roms"), result.AsError.Reason);
        Assert.True(File.Exists(PathOf("Roms", "rom.bin")));
    }

    [Fact]
    public void ProjectNodeViewModel_ExistingTree_MatchesSorted()
    {
        AddDataFile(_tree.Root, "data");
        var folder = AddFolder(_tree.Root, "Folder");
        AddDataFile(folder, "nested");

        var vm = new ProjectNodeViewModel(_tree);

        AssertMatchesTree(vm, _tree.Root);
        Assert.IsType<FolderNodeViewModel>(vm.Children[0]);
    }

    [Fact]
    public async Task ServiceOperations_KeepViewModelTreeInSync()
    {
        var projectVm = new ProjectNodeViewModel(_tree);

        var data = AddDataFile(_tree.Root, "b-data");
        AssertMatchesTree(projectVm, _tree.Root);

        var folder = AddFolder(_tree.Root, "Folder");
        AssertMatchesTree(projectVm, _tree.Root);
        Assert.Same(folder, projectVm.Children[0].Node);

        var palette = AddPalette(_tree.Root, "c-palette", (DataSource)data.Item, new ColorRgba32(1, 2, 3, 255));
        AddDataFile(_tree.Root, "d-data");
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

        var plan = _service.PreviewResourceDeletion(data);
        Assert.True(_service.ApplyResourceDeletion(plan, _palette).HasSucceeded);
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

    private static T ItemOf<T>(ProjectTree tree, string name) =>
        Assert.IsType<T>(tree.Root.ChildNodes.Single(x => x.Name == name).Item);

    [Fact]
    public async Task MoveNode_NodeWithoutDiskLocation_FailsWithoutThrowing()
    {
        var node = AddDataFile(_tree.Root, "data");
        var destination = AddFolder(_tree.Root, "Dest");
        node.DiskLocation = null;

        var result = await _service.MoveNodeAsync(node, destination);

        Assert.True(result.HasFailed);
        Assert.Same(_tree.Root, node.Parent);
    }

    [Fact]
    public async Task OpenProject_MissingDataFile_LoadsWithSourceMarkedMissing()
    {
        var data = AddDataFile(_tree.Root, "data");
        AddPalette(_tree.Root, "pal", (DataSource)data.Item, new ColorRgba32(1, 2, 3, 255));
        File.Delete(PathOf("rom.bin"));

        var tree = await ReopenAsync();

        var source = ItemOf<FileDataSource>(tree, "data");
        Assert.True(source.IsMissing);
        ItemOf<Palette>(tree, "pal");
    }

    [Fact]
    public async Task Relink_CopiesFileToExpectedLocation_AndRaisesResourceChanged()
    {
        AddDataFile(_tree.Root, "data");
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
        var data = AddDataFile(_tree.Root, "data");

        var result = await _service.RelinkDataFileAsync((FileDataSource)data.Item, PathOf("rom.bin"));

        Assert.True(result.HasFailed);
    }
}