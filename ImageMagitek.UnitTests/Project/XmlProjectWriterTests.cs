using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Services;
using Xunit;

namespace ImageMagitek.UnitTests.Project;

public sealed class XmlProjectWriterTests : IAsyncLifetime
{
    private readonly string _directory;
    private readonly ColorFactory _colorFactory = new();
    private readonly ProjectService _service;
    private ProjectTree _tree = null!;
    private DataSource _data = null!;

    public XmlProjectWriterTests()
    {
        _directory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(PathOf("rom.bin"), new byte[64]);

        _service = new ProjectService(ProjectFixtures.CreateSerializerFactory(_colorFactory));
    }

    public async Task InitializeAsync()
    {
        _tree = (await _service.CreateNewProjectAsync(PathOf("project.xml"))).AsSuccess.Result;
        _data = (DataSource)(await _service.AddResourceAsync(_tree.Root, new FileDataSource("rom", PathOf("rom.bin")))).AsSuccess.Result.Item;
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

    private string PathOf(string name) => Path.Combine(_directory, name);

    private async Task<Palette> AddPaletteAsync(params IColorSource[] sources)
    {
        var palette = new Palette("pal", _colorFactory, ColorModel.Bgr15, sources, false, PaletteStorageSource.ProjectXml, _data);
        Assert.True((await _service.AddResourceAsync(_tree.Root, palette)).HasSucceeded);
        return palette;
    }

    [Fact]
    public async Task FileSource_BitOffset_Written()
    {
        await AddPaletteAsync(new FileColorSource(new BitAddress(2, 4), Endian.Little), new FileColorSource(new BitAddress(4, 4), Endian.Little));

        var source = Assert.Single(XDocument.Load(PathOf("pal.xml")).Root!.Elements("filesource"));

        Assert.Equal("2", source.Attribute("fileoffset")?.Value);
        Assert.Equal("4", source.Attribute("bitoffset")?.Value);
        Assert.Equal("2", source.Attribute("entries")?.Value);
    }

    [Fact]
    public async Task ForeignColor_ModelMismatch_FailsSave()
    {
        var palette = await AddPaletteAsync(new ProjectNativeColorSource(new ColorRgba32(0, 0, 0, 255)), new ProjectForeignColorSource(new ColorBgr15(0x7C1F)));
        var before = File.ReadAllText(PathOf("pal.xml"));
        ((ProjectForeignColorSource)palette.ColorSources[1]).Value = new ColorRgba32(1, 2, 3, 255);

        var result = await _service.SaveProjectAsync(_tree);

        Assert.True(result.HasFailed);
        Assert.Contains("'pal'", result.AsError.Reason);
        Assert.Contains("color 1", result.AsError.Reason);
        Assert.Equal(before, File.ReadAllText(PathOf("pal.xml")));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp").Concat(Directory.GetFiles(_directory, "_transaction.json")));
    }
}
