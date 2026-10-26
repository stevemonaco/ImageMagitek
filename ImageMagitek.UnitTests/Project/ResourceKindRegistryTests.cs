using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Compression;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using ImageMagitek.UnitTests.Fixtures;
using Monaco.PathTree;
using Xunit;

namespace ImageMagitek.UnitTests.Project;

public sealed class ResourceKindRegistryTests : IDisposable
{
    private readonly string _directory;
    private readonly ColorFactory _colorFactory = new();

    public ResourceKindRegistryTests()
    {
        _directory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }
    }

    public static TheoryData<string> BuiltInElementNames() => [.. ResourceKindRegistry.BuiltIn.Kinds.Select(x => x.ElementName)];

    [Theory]
    [MemberData(nameof(BuiltInElementNames))]
    public void WriteThenRead_RoundTripsTheSample(string elementName)
    {
        var kind = ResourceKindRegistry.BuiltIn.FindByElement(elementName)!;
        var model = Sample(kind.ModelType);
        var errors = new List<string>();
        var context = new ResourceReadContext(Path.Combine(_directory, $"{model.Name}.xml"), _directory, _colorFactory, ProjectFixtures.DefaultPalette, errors);

        var element = kind.Write(model, _directory, _colorFactory);
        var read = kind.Read(element, context);

        Assert.Equal(elementName, element.Name.LocalName);
        Assert.Empty(errors);
        Assert.Equal(model, read);
    }

    [Fact]
    public void BuiltIn_CoversEveryResourceModel()
    {
        var kinds = ResourceKindRegistry.BuiltIn.Kinds;
        var resourceModels = ModelSamples.Types
            .Where(x => x.IsSubclassOf(typeof(ResourceModel)) && x != typeof(ImageProjectModel) && x != typeof(ResourceFolderModel));

        Assert.Equal(resourceModels.OrderBy(x => x.Name), kinds.Select(x => x.ModelType).OrderBy(x => x.Name));
    }

    [Fact]
    public void Constructor_DuplicateElementName_Throws()
    {
        var palette = ResourceKindRegistry.BuiltIn.FindByElement("palette")!;

        Assert.Throws<ArgumentException>(() => new ResourceKindRegistry([palette, new FakeKind("palette")]));
    }

    [Fact]
    public void BuiltIn_IsInBuildOrder()
    {
        Assert.Equal(["datafile", "compresseddata", "palette", "arranger", "metatileset", "tilemap", "metatilemap"],
            ResourceKindRegistry.BuiltIn.Kinds.Select(x => x.ElementName));
    }

    [Fact]
    public void FindByResource_WalksBaseTypes()
    {
        var file = new FileDataSource("rom", Path.Combine(_directory, "rom.bin"));
        var compressed = new CompressedDataSource("packed", file, 0, "Lz77", new CompressorFactory());
        var memory = new MemoryDataSource("memory", 64);
        var sequential = new SequentialArranger(1, 1, memory, ProjectFixtures.DefaultPalette, CodecFixture.Shared.CodecFactory,
            CodecFixture.Shared.CodecFactory.CreateCodec("NES 1bpp")!);

        Assert.Equal("datafile", ResourceKindRegistry.BuiltIn.FindByResource(file)?.ElementName);
        Assert.Equal("datafile", ResourceKindRegistry.BuiltIn.FindByResource(memory)?.ElementName);
        Assert.Equal("compresseddata", ResourceKindRegistry.BuiltIn.FindByResource(compressed)?.ElementName);
        Assert.Null(ResourceKindRegistry.BuiltIn.FindByResource(sequential));
    }

    [Fact]
    public async Task FakeKind_ReadsWritesBuildsAndRaisesTheVersion_WithOnlyItsDescriptor()
    {
        var kinds = new ResourceKindRegistry([.. ResourceKindRegistry.BuiltIn.Kinds, new FakeKind("fake")]);
        var factory = new XmlProjectSerializerFactory(ProjectFixtures.ResourceSchemaPath, CodecFixture.Shared.CodecFactory, _colorFactory,
            [ProjectFixtures.DefaultPalette], kinds: kinds);
        var service = new ProjectService(factory);
        var projectFileName = Path.Combine(_directory, "project.xml");

        var tree = (await service.CreateNewProjectAsync(projectFileName)).AsSuccess.Result;
        Assert.True((await service.AddResourceAsync(tree.Root, new FakeResource("thing", "hello"))).HasSucceeded);
        service.CloseProjects();

        Assert.Equal("1.1", XDocument.Load(projectFileName).Root!.Attribute("version")?.Value);
        Assert.Equal("hello", XDocument.Load(Path.Combine(_directory, "thing.xml")).Root!.Attribute("value")?.Value);

        var readResult = new XmlProjectReader(null!, CodecFixture.Shared.CodecFactory, _colorFactory, [ProjectFixtures.DefaultPalette], kinds: kinds)
            .ReadProject(projectFileName);
        var readTree = readResult.AsSuccess.Result;
        var node = Assert.IsType<FakeNode>(readTree.Root.ChildNodes.Single());
        Assert.Equal(new FakeModel { Name = "thing", Value = "hello" }, node.Model);

        var buildResult = factory.BuildResources(readTree, [(new FakeModel { Name = "built", Value = "x" }, "", Path.Combine(_directory, "built.xml"))]);
        Assert.True(buildResult.HasSucceeded);
        Assert.Equal("x", Assert.IsType<FakeResource>(readTree.Root.ChildNodes.Single(x => x.Name == "built").Item).Value);
    }

    [Fact]
    public async Task CreateNewProject_XmlOfACustomKind_ConflictsOnlyUnderThatRegistry()
    {
        File.WriteAllText(Path.Combine(_directory, "thing.xml"), "<fake value=\"hello\" />");
        var kinds = new ResourceKindRegistry([.. ResourceKindRegistry.BuiltIn.Kinds, new FakeKind("fake")]);
        var custom = new ProjectService(new XmlProjectSerializerFactory(ProjectFixtures.ResourceSchemaPath, CodecFixture.Shared.CodecFactory, _colorFactory,
            [ProjectFixtures.DefaultPalette], kinds: kinds));
        var builtIn = new ProjectService(new XmlProjectSerializerFactory(ProjectFixtures.ResourceSchemaPath, CodecFixture.Shared.CodecFactory, _colorFactory,
            [ProjectFixtures.DefaultPalette]));

        var conflict = await custom.CreateNewProjectAsync(Path.Combine(_directory, "custom.xml"));
        var created = await builtIn.CreateNewProjectAsync(Path.Combine(_directory, "builtin.xml"));
        builtIn.CloseProjects();

        Assert.Contains("thing.xml", conflict.AsError.Reason);
        Assert.False(File.Exists(Path.Combine(_directory, "custom.xml")));
        Assert.True(created.HasSucceeded, created.HasFailed ? created.AsError.Reason : null);
    }

    private ResourceModel Sample(Type modelType) => ModelSamples.Create(modelType) switch
    {
        DataFileModel x => x with { Location = Path.Combine(_directory, x.Location) },
        // An indexed arranger always writes a palette for each element, so a null key does not survive a round trip
        ScatteredArrangerModel x => x with { ElementGrid = WithPalettes(x.ElementGrid) },
        var x => (ResourceModel)x
    };

    private static ArrangerElementModel[,] WithPalettes(ArrangerElementModel[,] grid)
    {
        var copy = (ArrangerElementModel[,])grid.Clone();
        for (var y = 0; y < copy.GetLength(1); y++)
        {
            for (var x = 0; x < copy.GetLength(0); x++)
            {
                if (copy[x, y] is { PaletteKey: null } el)
                    copy[x, y] = el with { PaletteKey = "/Other" };
            }
        }

        return copy;
    }

    private sealed class FakeResource(string name, string value) : IProjectResource
    {
        public string Name { get; set; } = name;
        public string Value { get; } = value;
        public bool CanContainChildResources => false;
        public bool ShouldBeSerialized { get; set; } = true;
        public IEnumerable<ResourceEdge> Dependencies => [];
    }

    private sealed record FakeModel : ResourceModel
    {
        public required string Value { get; init; }
    }

    private sealed class FakeNode(string nodeName, FakeResource resource) : ResourceNode<FakeModel>(nodeName, resource);

    private sealed class FakeKind(string elementName) : ResourceKind<FakeResource, FakeModel, FakeNode>
    {
        public override string ElementName => elementName;
        public override Version MinimumVersion => new(1, 1);

        public override ResourceNode CreateNode(IProjectResource resource) => new FakeNode(resource.Name, (FakeResource)resource);

        internal override ResourceModel? MapToModel(ResourceNode node, Dictionary<IProjectResource, string> resourceMap, IColorFactory colorFactory) =>
            node.Item is FakeResource fake ? new FakeModel { Name = fake.Name, Value = fake.Value } : null;

        internal override ResourceModel Read(XElement root, ResourceReadContext context) =>
            new FakeModel { Name = context.Name, Value = root.Attribute("value")!.Value };

        internal override XElement Write(ResourceModel model, string resourceBaseDirectory, IColorFactory colorFactory) =>
            new(ElementName, new XAttribute("value", ((FakeModel)model).Value));

        internal override MagitekResults Build(ProjectTreeBuilder builder, ResourceModel model, string parentNodePath, string fileLocation)
        {
            var fake = (FakeModel)model;
            var node = new FakeNode(fake.Name, new FakeResource(fake.Name, fake.Value)) { DiskLocation = fileLocation, Model = fake };
            return MagitekResults.From(builder.AttachNode(node, parentNodePath));
        }
    }
}
