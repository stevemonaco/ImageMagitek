using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.Utility;
using Monaco.PathTree;
using Xunit;

namespace ImageMagitek.UnitTests.Project;

public sealed class XmlProjectRoundTripTests : IDisposable
{
    private readonly string _directory;
    private readonly ColorFactory _colorFactory = new();
    private readonly XmlProjectSerializerFactory _serializerFactory;
    private readonly ProjectService _service;

    public XmlProjectRoundTripTests()
    {
        _directory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(_directory);
        _serializerFactory = ProjectFixtures.CreateSerializerFactory(_colorFactory);
        _service = new ProjectService(_serializerFactory);
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

    public static TheoryData<string> Fixtures => new() { "FF2.zip", "Crystalis.zip", "CT BOSSX.zip", ProjectFixtures.AllFeatures };

    private string ResourcesPath => Path.Combine(_directory, "Resources");

    private async Task<ProjectTree> OpenAsync(string projectFile)
    {
        var result = await _service.OpenProjectFileAsync(projectFile);
        Assert.True(result.HasSucceeded, result.HasFailed ? string.Join("; ", result.AsError.Reasons) : null);
        return result.AsSuccess.Result;
    }

    private async Task<ProjectTree> ReopenAsync(ProjectTree tree)
    {
        var projectFile = tree.Root.DiskLocation!;
        _service.CloseProject(tree);
        return await OpenAsync(projectFile);
    }

    private static IEnumerable<ResourceNode> ResourceNodes(ProjectTree tree) =>
        tree.EnumerateDepthFirst().Where(x => x is not ResourceFolderNode);

    private Dictionary<string, ResourceModel> MapResources(ProjectTree tree)
    {
        var resourceMap = new Dictionary<IProjectResource, string> { [ProjectFixtures.DefaultPalette] = ProjectFixtures.DefaultPalette.Name };
        foreach (var node in ResourceNodes(tree))
            resourceMap.Add(node.Item, tree.CreatePathKey(node));

        return ResourceNodes(tree).ToDictionary(tree.CreatePathKey, node => node.Item switch
        {
            ImageProject project => (ResourceModel)project.MapToModel(),
            FileDataSource df => df.MapToModel(),
            Palette palette => palette.MapToModel(resourceMap, _colorFactory),
            ScatteredArranger arranger => arranger.MapToModel(resourceMap),
            _ => throw new InvalidOperationException($"Unexpected resource '{node.Item.GetType()}'")
        });
    }

    private static void AssertResourcesEqual(Dictionary<string, ResourceModel> expected, Dictionary<string, ResourceModel> actual)
    {
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        Assert.All(expected, x => Assert.True(x.Value.ResourceEquals(actual[x.Key]), $"'{x.Key}' changed"));
    }

    private async Task WriteAllAsync(ProjectTree tree)
    {
        var writer = _serializerFactory.CreateWriter(tree);
        foreach (var node in ResourceNodes(tree))
        {
            var result = await writer.WriteResourceAsync(node, true);
            Assert.True(result.HasSucceeded, result.HasFailed ? result.AsError.Reason : null);
        }
    }

    private Dictionary<string, (byte[] Contents, DateTime Written)> SnapshotFiles() =>
        Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories)
            .ToDictionary(x => x, x => (File.ReadAllBytes(x), File.GetLastWriteTimeUtc(x)));

    private List<string> ChangedFiles(Dictionary<string, (byte[] Contents, DateTime Written)> before)
    {
        var after = SnapshotFiles();
        Assert.Equal(before.Keys.Order(), after.Keys.Order());

        return before.Where(x => !x.Value.Contents.AsSpan().SequenceEqual(after[x.Key].Contents) || x.Value.Written != after[x.Key].Written)
            .Select(x => x.Key)
            .Order()
            .ToList();
    }

    private static readonly string[] RetiredCodecNames = ["SNES 3bpp", "PSX 4bpp", "PSX 8bpp"];

    private static bool NamesRetiredCodec(string xmlFile) =>
        XDocument.Parse(File.ReadAllText(xmlFile)).Descendants()
            .SelectMany(x => x.Attributes())
            .Any(x => x.Name.LocalName is "codec" or "defaultcodec" && RetiredCodecNames.Contains(x.Value));

    private async Task<ProjectTree> ReadWriteReadAsync(string projectFile)
    {
        var tree = await OpenAsync(projectFile);
        var expected = MapResources(tree);

        await WriteAllAsync(tree);
        var reopened = await ReopenAsync(tree);

        AssertResourcesEqual(expected, MapResources(reopened));
        return reopened;
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task ReadWriteRead_ResourcesEqual(string fixture)
    {
        await ReadWriteReadAsync(ProjectFixtures.Create(fixture, _directory));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task WriteTwice_ByteIdentical(string fixture)
    {
        var tree = await OpenAsync(ProjectFixtures.Create(fixture, _directory));
        await WriteAllAsync(tree);
        var reopened = await ReopenAsync(tree);
        var writer = _serializerFactory.CreateWriter(reopened);

        Assert.All(ResourceNodes(reopened), node =>
            Assert.Equal(File.ReadAllBytes(node.DiskLocation!), new UTF8Encoding(false).GetBytes(writer.SerializeResource(node))));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task SaveUnchanged_WritesOnlyRetiredCodecArrangers(string fixture)
    {
        var tree = await OpenAsync(ProjectFixtures.Create(fixture, _directory));
        var expected = ResourceNodes(tree).OfType<ArrangerNode>()
            .Select(x => x.DiskLocation!)
            .Where(NamesRetiredCodec)
            .Order()
            .ToList();
        var before = SnapshotFiles();

        var result = await _service.SaveProjectAsync(tree);

        Assert.True(result.HasSucceeded);
        Assert.Equal(expected, ChangedFiles(before));
    }

    private void AssertRootResourcesIntact(ProjectTree reopened)
    {
        Assert.Equal("main.bin", XDocument.Load(Path.Combine(ResourcesPath, "main.xml")).Root!.Attribute("location")!.Value);
        Assert.All(ResourceNodes(reopened).Select(x => x.Item).OfType<FileDataSource>(), x => Assert.False(x.IsMissing));
    }

    [Fact]
    public async Task RootRelative_SaveAndReopen_Unchanged()
    {
        var reopened = await ReadWriteReadAsync(ProjectFixtures.Create(ProjectFixtures.AllFeatures, _directory));

        AssertRootResourcesIntact(reopened);
    }

    [Fact]
    public async Task RootAbsolute_SaveAndReopen_Unchanged()
    {
        var projectFile = ProjectFixtures.Create(ProjectFixtures.AllFeatures, _directory);
        File.WriteAllText(projectFile, $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<project version=\"0.9\" root=\"{ResourcesPath}\" />");

        var reopened = await ReadWriteReadAsync(projectFile);

        AssertRootResourcesIntact(reopened);
    }

    [Fact]
    public async Task WalRecoveredSave_LoadsCommittedContent()
    {
        var projectFile = ProjectFixtures.Create(ProjectFixtures.AllFeatures, _directory);
        var palettePath = Path.Combine(ResourcesPath, "Palettes", "Nested", "Alt.xml");
        var arrangerPath = Path.Combine(ResourcesPath, "Graphics", "Direct.xml");
        File.WriteAllText(palettePath + ".tmp", File.ReadAllText(palettePath).Replace("#000000FF", "#11223344"));
        File.WriteAllText(arrangerPath + ".tmp", File.ReadAllText(arrangerPath).Replace("fileoffset=\"100\"", "fileoffset=\"200\""));

        var journal = new WalJournal
        {
            CreatedUtc = DateTime.UtcNow,
            Operations = [.. new[] { palettePath, arrangerPath }.Select(path => new WalOperation
            {
                Id = Guid.NewGuid(),
                Type = WalOperationType.WriteFile,
                TargetPath = path,
                StagingPath = path + ".tmp",
                BackupPath = path + ".bak",
                State = WalOperationState.Pending
            })]
        };
        File.WriteAllText(Path.Combine(_directory, "_transaction.json"), JsonSerializer.Serialize(journal));

        var tree = await OpenAsync(projectFile);

        Assert.False(File.Exists(Path.Combine(_directory, "_transaction.json")));
        Assert.True(tree.TryGetItem<Palette>("/Palettes/Nested/Alt", out var palette));
        Assert.Equal(new ColorRgba32(0x11, 0x22, 0x33, 0x44), ((ProjectNativeColorSource)palette.ColorSources[0]).Value);
        Assert.True(tree.TryGetItem<ScatteredArranger>("/Graphics/Direct", out var arranger));
        Assert.Equal(new BitAddress(0x200, 0), arranger.GetElement(1, 0)!.Value.SourceAddress);
    }
}
