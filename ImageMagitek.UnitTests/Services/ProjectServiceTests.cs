using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.UnitTests.Fixtures;
using ImageMagitek.UnitTests.TestFactories;
using Xunit;

namespace ImageMagitek.UnitTests.Services;

public sealed class ProjectServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly Palette _palette = ArrangerTestFactory.CreatePalette(new ColorRgba32(0, 0, 0, 255));
    private readonly ProjectService _service;
    private readonly ProjectTree _tree;

    public ProjectServiceTests()
    {
        _directory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "rom.bin"), new byte[64]);

        var colorFactory = new ColorFactory();
        var serializerFactory = new XmlProjectSerializerFactory(Path.Combine(AppContext.BaseDirectory, "_schemas", "ResourceSchema.xsd"),
            CodecFixture.Shared.CodecFactory, colorFactory, [_palette]);

        _service = new ProjectService(serializerFactory, colorFactory);
        _tree = _service.CreateNewProject(Path.Combine(_directory, "project.xml")).AsSuccess.Result;
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

    private string PathOf(params string[] parts) => Path.Combine([_directory, .. parts]);

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
    }

    [Fact]
    public void DeleteFolder_WithNestedFolder_RemovesBothDirectories()
    {
        var outer = AddFolder(_tree.Root, "Outer");
        var inner = AddFolder(outer, "Inner");
        AddDataFile(inner, "data");

        var changes = _service.PreviewResourceDeletionChanges(outer).ToList();
        var result = _service.ApplyResourceDeletionChanges(changes, _palette);

        Assert.True(result.HasSucceeded);
        Assert.False(Directory.Exists(PathOf("Outer", "Inner")));
        Assert.False(Directory.Exists(PathOf("Outer")));
    }

    [Fact]
    public void DeleteFolder_WithUnmanagedFile_KeepsDirectoryAndReportsIt()
    {
        var folder = AddFolder(_tree.Root, "Roms");
        File.WriteAllBytes(PathOf("Roms", "rom.bin"), new byte[4]);

        var changes = _service.PreviewResourceDeletionChanges(folder).ToList();
        var result = _service.ApplyResourceDeletionChanges(changes, _palette);

        Assert.True(result.HasFailed);
        Assert.Contains(PathOf("Roms"), result.AsError.Reason);
        Assert.True(File.Exists(PathOf("Roms", "rom.bin")));
    }
}
