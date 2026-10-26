using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagitek.Colors;
using ImageMagitek.PluginSamples;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using TileShop.CLI;
using Xunit;

namespace ImageMagitek.UnitTests.Cli;

public sealed class CliApplicationTests : IDisposable
{
    private const string DirectKey = "Graphics/Direct";
    private const string SimpleKey = "Graphics/Sprites/Simple";
    private const string FontKey = "Graphics/Font";

    private static readonly Rgba32 Black = new(0, 0, 0, 255);
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    private readonly string _directory;
    private readonly string _projectFile;
    private readonly string _imageDirectory;
    private readonly string _defaultLogFile;
    private readonly StringWriter _output = new();
    private readonly ColorFactory _colorFactory = new();
    private Func<ILoggerFactory, (IProjectService, IResourceGenerationService)> _bootstrap;

    public CliApplicationTests()
    {
        _directory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(_directory);
        _projectFile = ProjectFixtures.Create(ProjectFixtures.AllFeatures, _directory);
        _imageDirectory = Path.Combine(_directory, "images");
        _defaultLogFile = Path.Combine(_directory, "app", "errorlogCLI.txt");
        _bootstrap = _ => Services(ProjectFixtures.CreateSerializerFactory(_colorFactory), x => new ProjectService(x));

        WriteResource("Palettes/Simple.xml", """
            <palette datafile="/main" color="Rgba32" zeroindextransparent="false">
            	<nativecolor value="#000000FF" />
            	<nativecolor value="#FFFFFFFF" />
            	<nativecolor value="#FF0000FF" />
            	<nativecolor value="#0000FFFF" />
            </palette>
            """);
        WriteResource("Data/simple.xml", """<datafile location="simple.bin" />""");
        File.Delete(Path.Combine(_directory, "Resources", "Graphics", "Sprites", "Indexed.xml"));
        WriteResource("Graphics/Sprites/Simple.xml", """
            <arranger elementsx="2" elementsy="1" width="8" height="8" layout="tiled" color="indexed" defaultcodec="SNES 2bpp" defaultdatafile="/Data/simple" defaultpalette="/Palettes/Simple">
            	<element fileoffset="0" posx="0" posy="0" />
            	<element fileoffset="10" posx="1" posy="0" />
            </arranger>
            """);
        File.WriteAllBytes(SimpleDataFile, new byte[0x600]);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A plugin load context is not collectible, so a copied plugin DLL stays locked
        }
    }

    private string SimpleDataFile => Path.Combine(_directory, "Resources", "simple.bin");

    private void WriteResource(string relativePath, string xml)
    {
        var path = Path.Combine(_directory, "Resources", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n{xml}");
    }

    private Task<int> RunAsync(params string[] args) =>
        new CliApplication(_output, _bootstrap, _defaultLogFile).RunAsync(args);

    private string Output => _output.ToString();

    private string ImagePath(string key, string? directory = null) =>
        Path.Combine([directory ?? _imageDirectory, .. key.Split('/')]) + ".png";

    private async Task ExportSimpleAsync()
    {
        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, SimpleKey));
    }

    private void EditPixel(string key, Rgba32 color)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(ImagePath(key));
        image[0, 0] = color;
        image.SaveAsPng(ImagePath(key), new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
    }

    private async Task<Rgba32> ReexportPixelAsync(string key)
    {
        var directory = Path.Combine(_directory, "reexport");
        Assert.Equal(0, await RunAsync("export", _projectFile, directory, key, "--overwrite"));
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(ImagePath(key, directory));
        return image[0, 0];
    }

    private void UsePluginEnvironment()
    {
        var pluginsPath = Path.Combine(_directory, "_plugins");
        var samplesAssembly = typeof(LastArmageddonCodec).Assembly;
        var pluginName = samplesAssembly.GetName().Name!;
        var pluginDirectory = Directory.CreateDirectory(Path.Combine(pluginsPath, pluginName));
        File.Copy(samplesAssembly.Location, Path.Combine(pluginDirectory.FullName, pluginName + ".dll"));

        WriteResource("Graphics/Font.xml", """
            <arranger elementsx="1" elementsy="1" width="256" height="8" layout="tiled" color="indexed" defaultcodec="Last Armageddon Font" defaultdatafile="/Data/simple" defaultpalette="/Palettes/Simple">
            	<element fileoffset="0" posx="0" posy="0" />
            </arranger>
            """);

        var paths = BootstrapPaths.FromDirectory(AppContext.BaseDirectory) with { PluginsPath = pluginsPath };
        _bootstrap = loggerFactory => CliBootstrap.CreateProjectServices(paths, loggerFactory, _output);
    }

    private static (IProjectService, IResourceGenerationService) Services(IProjectSerializerFactory serializerFactory, Func<ProjectWorkspace, ProjectService> createService)
    {
        var workspace = new ProjectWorkspace(serializerFactory);
        return (createService(workspace), new ResourceGenerationService(workspace));
    }

    private sealed class YieldingProjectService(ProjectWorkspace workspace) : ProjectService(workspace)
    {
        public override async Task<MagitekResults<ProjectTree>> OpenProjectFileAsync(string projectFileName)
        {
            await Task.Yield();
            return await base.OpenProjectFileAsync(projectFileName);
        }
    }

    private sealed class ThrowingProjectService(ProjectWorkspace workspace) : ProjectService(workspace)
    {
        public override Task<MagitekResults<ProjectTree>> OpenProjectFileAsync(string projectFileName) =>
            throw new InvalidOperationException("Open failed on purpose");
    }

    #region Invocation and exit codes

    [Fact]
    public async Task OpenYields_ReturnsVerbCode()
    {
        _bootstrap = _ => Services(ProjectFixtures.CreateSerializerFactory(_colorFactory), x => new YieldingProjectService(x));

        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, SimpleKey));
        Assert.True(File.Exists(ImagePath(SimpleKey)));
        Assert.Contains("Operation completed successfully", Output);
    }

    [Fact]
    public async Task InvalidArguments_Exits3()
    {
        Assert.Equal(-3, await RunAsync("export", _projectFile));
        Assert.Contains("Operation failed due to invalid command line options", Output);
        Assert.Contains("Usage:", Output);
    }

    [Fact]
    public async Task Help_Exits0WithoutStatusLine()
    {
        Assert.Equal(0, await RunAsync("--help"));
        Assert.Contains("exportall", Output);
        Assert.DoesNotContain("Operation", Output);
        Assert.False(Directory.Exists(Path.GetDirectoryName(_defaultLogFile)));
    }

    [Fact]
    public async Task VerbHelp_Exits0()
    {
        Assert.Equal(0, await RunAsync("import", "--help"));
        Assert.Contains("--transparent-index0", Output);
        Assert.DoesNotContain("Operation", Output);
    }

    [Fact]
    public async Task Version_Exits0()
    {
        Assert.Equal(0, await RunAsync("--version"));
        Assert.DoesNotContain("Operation", Output);
    }

    [Fact]
    public async Task PascalCaseVerb_Parses()
    {
        Assert.Equal(0, await RunAsync("ExportAll", _projectFile, _imageDirectory));
    }

    [Fact]
    public async Task UnknownVerbCasing_Exits3()
    {
        Assert.Equal(-3, await RunAsync("EXPORTALL", _projectFile, _imageDirectory));
    }

    [Fact]
    public async Task EnvironmentFails_Exits4()
    {
        _bootstrap = _ => throw new BootstrapException("No global palette could be loaded");

        Assert.Equal(-4, await RunAsync("print", _projectFile));
        Assert.Contains("No global palette could be loaded", Output);
        Assert.Contains("TileShop environment could not be loaded", Output);
    }

    [Fact]
    public async Task BootstrapIssue_PrintsWarningExits0()
    {
        var codecsPath = Path.Combine(_directory, "_codecs");
        Directory.CreateDirectory(codecsPath);
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "_codecs")))
            File.Copy(file, Path.Combine(codecsPath, Path.GetFileName(file)));
        File.WriteAllText(Path.Combine(codecsPath, "Broken.xml"), "<not a codec");
        var paths = BootstrapPaths.FromDirectory(AppContext.BaseDirectory) with { CodecsPath = codecsPath };
        _bootstrap = loggerFactory => CliBootstrap.CreateProjectServices(paths, loggerFactory, _output);

        Assert.Equal(0, await RunAsync("print", _projectFile));
        var lines = Output.Split(Environment.NewLine);
        Assert.StartsWith("TileShopCLI", lines[0]);
        Assert.DoesNotContain("AllFeatures:", lines[1]);
        Assert.Contains("AllFeatures: Type 'ImageProject'", Output);
    }

    [Fact]
    public async Task ProjectFileMissing_Exits5()
    {
        Assert.Equal(-5, await RunAsync("print", Path.Combine(_directory, "missing.xml")));
        Assert.Contains("contained 1 errors", Output);
    }

    [Fact]
    public async Task ProjectNamesUnknownCodec_Exits5()
    {
        var arrangerFile = Path.Combine(_directory, "Resources", "Graphics", "Sprites", "Simple.xml");
        File.WriteAllText(arrangerFile, File.ReadAllText(arrangerFile).Replace("SNES 2bpp", "No Such Codec"));

        Assert.Equal(-5, await RunAsync("print", _projectFile));
    }

    [Fact]
    public async Task NewerFormat_Exits5()
    {
        File.WriteAllText(_projectFile, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<project version=\"1.2\" root=\"Resources\" />");

        Assert.Equal(-5, await RunAsync("print", _projectFile));
        Assert.Contains("1.2", Output);
    }

    [Fact]
    public async Task OpenThrows_Exits2()
    {
        _bootstrap = _ => Services(ProjectFixtures.CreateSerializerFactory(_colorFactory), x => new ThrowingProjectService(x));

        Assert.Equal(-2, await RunAsync("print", _projectFile));
        Assert.Contains("Open failed on purpose", Output);
    }

    [Fact]
    public async Task Print_ListsEveryNode_Exits0()
    {
        Assert.Equal(0, await RunAsync("print", _projectFile));
        Assert.Contains("Simple: Type 'ScatteredArranger'; Resource Key '/Graphics/Sprites/Simple'", Output);
        Assert.Contains("Mixed: Type 'Palette'; Resource Key '", Output);
        Assert.Contains("main: Type 'FileDataSource'; Resource Key '", Output);
    }

    #endregion

    #region Logging

    [Fact]
    public async Task Log_ExplicitPath_WritesFailureToThatFile()
    {
        var logFile = Path.Combine(_directory, "logs", "custom.log");

        Assert.Equal(-5, await RunAsync("print", Path.Combine(_directory, "missing.xml"), "--log", logFile));
        Assert.Contains("Operation failed because the project could not be opened", File.ReadAllText(logFile));
        Assert.False(Directory.Exists(Path.GetDirectoryName(_defaultLogFile)));
    }

    [Fact]
    public async Task Log_Default_WritesUnderAppDirectory()
    {
        Assert.Equal(-5, await RunAsync("print", Path.Combine(_directory, "missing.xml")));

        var logFile = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(_defaultLogFile)!, "errorlogCLI*.txt"));
        Assert.Contains("Operation failed because the project could not be opened", File.ReadAllText(logFile));
    }

    [Fact]
    public async Task Log_Unwritable_RunsAsIfLogged()
    {
        var blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "");

        Assert.Equal(-5, await RunAsync("print", Path.Combine(_directory, "missing.xml"), "--log", Path.Combine(blocker, "x.log")));
        Assert.Equal(0, await RunAsync("print", _projectFile, "--log", Path.Combine(blocker, "x.log")));
    }

    #endregion

    #region Export

    [Fact]
    public async Task Export_NestedArranger_FreshDirectory_WritesPngExits0()
    {
        var directory = Path.Combine(_directory, "fresh", "nested");

        Assert.Equal(0, await RunAsync("export", _projectFile, directory, SimpleKey));
        Assert.True(File.Exists(ImagePath(SimpleKey, directory)));
        Assert.Contains("Completed successfully", Output);
    }

    [Fact]
    public async Task Export_Existing_WithoutOverwrite_SkipsExits0()
    {
        await ExportSimpleAsync();
        File.WriteAllText(ImagePath(SimpleKey), "marker");

        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, SimpleKey));
        Assert.Equal("marker", File.ReadAllText(ImagePath(SimpleKey)));
        Assert.Contains("File already exists and was skipped to not overwrite it", Output);
    }

    [Fact]
    public async Task Export_Existing_WithOverwrite_Replaces()
    {
        await ExportSimpleAsync();
        File.WriteAllText(ImagePath(SimpleKey), "marker");

        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, SimpleKey, "--overwrite"));
        Assert.NotEqual("marker", File.ReadAllText(ImagePath(SimpleKey)));
    }

    [Fact]
    public async Task Export_UnknownKey_ExportsOthersExits7()
    {
        Assert.Equal(-7, await RunAsync("export", _projectFile, _imageDirectory, "Graphics/Nope", SimpleKey));
        Assert.Contains("Exporting 'Graphics/Nope'...Resource key not found in project", Output);
        Assert.True(File.Exists(ImagePath(SimpleKey)));
        Assert.Contains("Operation failed due to an export error", Output);
    }

    [Fact]
    public async Task Export_NotScatteredArranger_Exits7()
    {
        Assert.Equal(-7, await RunAsync("export", _projectFile, _imageDirectory, "Palettes/Mixed"));
        Assert.Contains("Resource key is not a Scattered Arranger", Output);
    }

    [Fact]
    public async Task Export_UnwritablePng_ContinuesExits7()
    {
        Directory.CreateDirectory(ImagePath(DirectKey));

        Assert.Equal(-7, await RunAsync("export", _projectFile, _imageDirectory, DirectKey, SimpleKey, "--overwrite"));
        Assert.True(File.Exists(ImagePath(SimpleKey)));
    }

    [Fact]
    public async Task ExportAll_WritesEveryScatteredArranger()
    {
        Assert.Equal(0, await RunAsync("exportall", _projectFile, _imageDirectory));
        Assert.True(File.Exists(ImagePath(SimpleKey)));
        Assert.True(File.Exists(ImagePath(DirectKey)));
        Assert.Equal(2, Directory.GetFiles(_imageDirectory, "*.png", SearchOption.AllDirectories).Length);
    }

    private const string TilemapKey = "Graphics/Screen";

    private void UseTilemap()
    {
        WriteResource("Graphics/Screen.xml", """
            <tilemap mapdatafile="/Data/simple" mapoffset="100" width="2" height="1" format="GBA Text BG" tiledatafile="/Data/simple" tileoffset="0" codec="SNES 2bpp">
            	<paletteslot index="0" palette="/Palettes/Simple" />
            </tilemap>
            """);

        UseShippedMapFormats();
    }

    private void UseShippedMapFormats()
    {
        var serializerFactory = new XmlProjectSerializerFactory(ProjectFixtures.ResourceSchemaPath, Fixtures.CodecFixture.Shared.CodecFactory,
            _colorFactory, [ProjectFixtures.DefaultPalette], new ImageMagitek.Compression.CompressorFactory(), Tilemaps.MapFormatTests.LoadShippedFormats());
        _bootstrap = _ => Services(serializerFactory, x => new ProjectService(x));
    }

    private void WriteSimpleData(int offset, params byte[] bytes)
    {
        var data = File.ReadAllBytes(SimpleDataFile);
        bytes.CopyTo(data, offset);
        File.WriteAllBytes(SimpleDataFile, data);
    }

    [Fact]
    public async Task Export_Tilemap_WritesPngExits0()
    {
        UseTilemap();

        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, TilemapKey));
        Assert.True(File.Exists(ImagePath(TilemapKey)));
    }

    [Fact]
    public async Task ExportAll_IncludesTilemaps()
    {
        UseTilemap();

        Assert.Equal(0, await RunAsync("exportall", _projectFile, _imageDirectory));
        Assert.True(File.Exists(ImagePath(TilemapKey)));
        Assert.Equal(3, Directory.GetFiles(_imageDirectory, "*.png", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task Import_Tilemap_WritesTilesExits0()
    {
        UseTilemap();
        WriteSimpleData(0x100, 0x01, 0x00, 0x02, 0x00);
        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, TilemapKey));
        EditPixel(TilemapKey, White);

        Assert.Equal(0, await RunAsync("import", _projectFile, _imageDirectory, TilemapKey));
        Assert.Equal(White, await ReexportPixelAsync(TilemapKey));
        Assert.DoesNotContain("Shared tile", Output);
    }

    [Fact]
    public async Task Import_Tilemap_SharedTileConflict_PrintsConflictExits0()
    {
        UseTilemap();
        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, TilemapKey));
        EditPixel(TilemapKey, White);

        Assert.Equal(0, await RunAsync("import", _projectFile, _imageDirectory, TilemapKey));
        Assert.Contains("1 shared tile differs between cells and was not imported", Output);
        Assert.Contains("  Shared tile at 0x0 differs between 2 cells, first at (0, 0); not imported", Output);
        Assert.Equal(Black, await ReexportPixelAsync(TilemapKey));
    }

    [Fact]
    public async Task Import_TilemapWithMapError_PrintsErrorExits6()
    {
        UseTilemap();
        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, TilemapKey));
        var truncatedOffset = new FileInfo(SimpleDataFile).Length - 2;
        WriteResource("Graphics/Screen.xml", $"""
            <tilemap mapdatafile="/Data/simple" mapoffset="{truncatedOffset:X}" width="2" height="1" format="GBA Text BG" tiledatafile="/Data/simple" tileoffset="0" codec="SNES 2bpp">
            	<paletteslot index="0" palette="/Palettes/Simple" />
            </tilemap>
            """);

        Assert.Equal(-6, await RunAsync("import", _projectFile, _imageDirectory, TilemapKey));
        Assert.Contains("holds 1 of the 2 entries", Output);
    }

    [Fact]
    public async Task ImportAll_IncludesTilemaps()
    {
        UseTilemap();
        Assert.Equal(0, await RunAsync("exportall", _projectFile, _imageDirectory));

        Assert.Equal(0, await RunAsync("importall", _projectFile, _imageDirectory));
        Assert.Contains($"to '/{TilemapKey}'...Completed successfully", Output);
    }

    [Fact]
    public async Task ImportAll_CompressedTilemap_Skipped()
    {
        UseTilemap();
        WriteSimpleData(0x200, 0x10, 0x20, 0x00, 0x00);
        WriteResource("Data/packed.xml", """<compresseddata parent="/Data/simple" fileoffset="200" compression="GBA LZ77" />""");
        WriteResource("Graphics/PackedScreen.xml", """
            <tilemap mapdatafile="/Data/simple" mapoffset="100" width="2" height="1" format="GBA Text BG" tiledatafile="/Data/packed" tileoffset="0" codec="SNES 2bpp">
            	<paletteslot index="0" palette="/Palettes/Simple" />
            </tilemap>
            """);
        Assert.Equal(0, await RunAsync("exportall", _projectFile, _imageDirectory));

        Assert.Equal(0, await RunAsync("importall", _projectFile, _imageDirectory));
        Assert.Contains("to '/Graphics/PackedScreen'...Skipped: arranger is read-only (reads compressed data 'packed', which is read-only)", Output);
    }

    private const string MetatileMapKey = "Graphics/Town";

    private void UseMetatileMap(string mapOffset = "100")
    {
        UseTilemap();
        WriteResource("Graphics/TownTiles.xml", """
            <metatileset metatiledatafile="/Data/simple" metatileoffset="200" metatilecount="1" layers="2" format="GBA Text BG" tiledatafile="/Data/simple" tileoffset="0" codec="SNES 2bpp">
            	<paletteslot index="0" palette="/Palettes/Simple" />
            </metatileset>
            """);
        WriteResource("Graphics/Town.xml", $"""
            <metatilemap mapdatafile="/Data/simple" mapoffset="{mapOffset}" width="2" height="1" format="FRLG Block">
            	<set metatileset="/Graphics/TownTiles" base="0" />
            </metatilemap>
            """);
    }

    [Fact]
    public async Task Export_MetatileMap_WritesCompositePngExits0()
    {
        UseMetatileMap();

        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, MetatileMapKey));

        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(ImagePath(MetatileMapKey));
        Assert.Equal((32, 16), (image.Width, image.Height));
        Assert.Equal(Black, image[0, 0]);
    }

    [Fact]
    public async Task Export_MetatileMapWithMapError_Exits7()
    {
        UseMetatileMap($"{new FileInfo(SimpleDataFile).Length - 2:X}");

        Assert.Equal(-7, await RunAsync("export", _projectFile, _imageDirectory, MetatileMapKey));
        Assert.Contains("holds 1 of the 2 blocks", Output);
        Assert.False(File.Exists(ImagePath(MetatileMapKey)));
    }

    [Fact]
    public async Task ExportAll_IncludesMetatileMaps()
    {
        UseMetatileMap();

        Assert.Equal(0, await RunAsync("exportall", _projectFile, _imageDirectory));
        Assert.True(File.Exists(ImagePath(MetatileMapKey)));
        Assert.Equal(4, Directory.GetFiles(_imageDirectory, "*.png", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task Import_MetatileMap_SkippedExits0()
    {
        UseMetatileMap();
        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, MetatileMapKey));

        Assert.Equal(0, await RunAsync("import", _projectFile, _imageDirectory, MetatileMapKey));
        Assert.Contains("Skipped: metatile maps cannot be imported", Output);
    }

    [Fact]
    public async Task ImportAll_SkipsMetatileMaps()
    {
        UseMetatileMap();
        Assert.Equal(0, await RunAsync("exportall", _projectFile, _imageDirectory));

        Assert.Equal(0, await RunAsync("importall", _projectFile, _imageDirectory));
        Assert.Contains($"to '/{MetatileMapKey}'...Skipped: metatile maps cannot be imported", Output);
    }

    #endregion

    #region Missing data files

    [Fact]
    public async Task Export_MissingDataFile_NamesFileExits7()
    {
        File.Delete(SimpleDataFile);

        Assert.Equal(-7, await RunAsync("export", _projectFile, _imageDirectory, SimpleKey));
        Assert.Contains("Data file 'simple' is missing at '", Output);
        Assert.False(File.Exists(ImagePath(SimpleKey)));
    }

    [Fact]
    public async Task ExportAll_MissingDataFile_ExportsOthersExits7()
    {
        File.Delete(SimpleDataFile);

        Assert.Equal(-7, await RunAsync("exportall", _projectFile, _imageDirectory));
        Assert.False(File.Exists(ImagePath(SimpleKey)));
        Assert.True(File.Exists(ImagePath(DirectKey)));
    }

    [Fact]
    public async Task Export_IndexPastPalette_ReportsElementExits7()
    {
        const string indexedKey = "Graphics/Sprites/Indexed";
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Projects", ProjectFixtures.AllFeatures, "Resources", "Graphics", "Sprites", "Indexed.xml"),
            Path.Combine(_directory, "Resources", "Graphics", "Sprites", "Indexed.xml"));

        Assert.Equal(-7, await RunAsync("export", _projectFile, _imageDirectory, indexedKey));
        Assert.Contains("Cannot export 'Indexed': the element at", Output);
        Assert.False(File.Exists(ImagePath(indexedKey)));
    }

    [Fact]
    public async Task Import_MissingDataFile_NamesFileExits6()
    {
        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, SimpleKey));
        File.Delete(SimpleDataFile);

        Assert.Equal(-6, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey));
        Assert.Contains("Data file 'simple' is missing at '", Output);
    }

    [Fact]
    public async Task Import_MissingDataFile_WithF_StillExits6()
    {
        File.Delete(SimpleDataFile);

        Assert.Equal(-6, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey, "-f"));
        Assert.Contains("Data file 'simple' is missing at '", Output);
    }

    #endregion

    #region Import

    [Fact]
    public async Task Import_EditedPng_WritesDataFileExits0()
    {
        await ExportSimpleAsync();
        EditPixel(SimpleKey, White);
        var projectFiles = Directory.GetFiles(_directory, "*.xml", SearchOption.AllDirectories)
            .ToDictionary(x => x, File.ReadAllBytes);

        Assert.Equal(0, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey));

        Assert.Contains("Completed successfully (", Output);
        Assert.Contains(File.ReadAllBytes(SimpleDataFile), x => x != 0);
        foreach (var (file, bytes) in projectFiles)
            Assert.Equal(bytes, File.ReadAllBytes(file));
        Assert.Equal(White, await ReexportPixelAsync(SimpleKey));
    }

    [Fact]
    public async Task Import_UnmatchedColor_Exits6_DataUnchanged()
    {
        await ExportSimpleAsync();
        EditPixel(SimpleKey, new Rgba32(0, 255, 0, 255));

        Assert.Equal(-6, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey));
        Assert.DoesNotContain(File.ReadAllBytes(SimpleDataFile), x => x != 0);
    }

    [Fact]
    public async Task Import_MissingImage_Exits6()
    {
        Assert.Equal(-6, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey));
        Assert.Contains("File does not exist", Output);
    }

    [Fact]
    public async Task Import_UnreadableImage_Exits6()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ImagePath(SimpleKey))!);
        File.WriteAllText(ImagePath(SimpleKey), "not a png");

        Assert.Equal(-6, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey));
        Assert.DoesNotContain(File.ReadAllBytes(SimpleDataFile), x => x != 0);
    }

    [Fact]
    public async Task Import_MissingImage_WithF_Exits0()
    {
        Assert.Equal(0, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey, "-f"));
    }

    [Fact]
    public async Task Import_BadKey_Exits6()
    {
        Assert.Equal(-6, await RunAsync("import", _projectFile, _imageDirectory, "Graphics/Nope"));
        Assert.Contains("Resource key does not exist or is not a Scattered Arranger or Tilemap", Output);
    }

    [Fact]
    public async Task Import_BadKey_WithR_Exits0()
    {
        Assert.Equal(0, await RunAsync("import", _projectFile, _imageDirectory, "Graphics/Nope", "-r"));
    }

    [Fact]
    public async Task Import_StopsAtFirstFailure_EarlierArrangersWritten()
    {
        await ExportSimpleAsync();
        EditPixel(SimpleKey, White);

        Assert.Equal(-6, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey, "Graphics/Nope", DirectKey));
        Assert.Contains(File.ReadAllBytes(SimpleDataFile), x => x != 0);
        Assert.DoesNotContain($"to '{DirectKey}'", Output);
    }

    [Fact]
    public async Task Import_MatchNearest_SubstitutesExits0()
    {
        await ExportSimpleAsync();
        EditPixel(SimpleKey, new Rgba32(250, 250, 250, 255));

        Assert.Equal(0, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey, "--match", "nearest"));
        Assert.Equal(White, await ReexportPixelAsync(SimpleKey));
    }

    [Fact]
    public async Task Import_MaxDistance_RejectsFarColorExits6()
    {
        await ExportSimpleAsync();
        EditPixel(SimpleKey, new Rgba32(0, 255, 0, 255));

        Assert.Equal(-6, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey, "--match", "Nearest", "--max-distance", "1"));
        Assert.DoesNotContain(File.ReadAllBytes(SimpleDataFile), x => x != 0);
    }

    [Fact]
    public async Task Import_TransparentIndex0_MapsToIndexZero()
    {
        File.WriteAllBytes(SimpleDataFile, Enumerable.Repeat((byte)0xFF, 0x600).ToArray());
        await ExportSimpleAsync();
        EditPixel(SimpleKey, new Rgba32(255, 0, 255, 0));

        Assert.Equal(-6, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey));
        Assert.Equal(0, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey, "--transparent-index0"));
        Assert.Equal(Black, await ReexportPixelAsync(SimpleKey));
    }

    [Fact]
    public async Task Import_MaxDistanceWithExact_Exits3()
    {
        Assert.Equal(-3, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey, "--max-distance", "5"));
        Assert.Equal(-3, await RunAsync("importall", _projectFile, _imageDirectory, "--match", "nearest", "--max-distance", "-1"));
    }

    [Fact]
    public async Task Import_UndefinedNumericMatch_Exits3()
    {
        Assert.Equal(-3, await RunAsync("import", _projectFile, _imageDirectory, SimpleKey, "--match", "99", "--max-distance", "5"));
        Assert.Contains("--match must be exact, nearest or nearestrgb", Output);
    }

    #endregion

    #region Plugins and read-only arrangers

    [Fact]
    public async Task Export_ProjectUsingCodecPlugin_Exits0()
    {
        UsePluginEnvironment();

        Assert.Equal(0, await RunAsync("export", _projectFile, _imageDirectory, FontKey));
        Assert.True(File.Exists(ImagePath(FontKey)));
    }

    [Fact]
    public async Task ImportAll_ReadOnlyArranger_SkipsAndImportsRest()
    {
        UsePluginEnvironment();
        Assert.Equal(0, await RunAsync("exportall", _projectFile, _imageDirectory));
        EditPixel(SimpleKey, White);

        Assert.Equal(0, await RunAsync("importall", _projectFile, _imageDirectory));
        Assert.Contains("Skipped: arranger is read-only (uses codec 'Last Armageddon Font' that cannot encode)", Output);
        Assert.Contains(File.ReadAllBytes(SimpleDataFile), x => x != 0);
    }

    [Fact]
    public async Task ImportAll_ReadOnlyDataFile_Exits6()
    {
        Assert.Equal(0, await RunAsync("exportall", _projectFile, _imageDirectory));
        File.SetAttributes(SimpleDataFile, FileAttributes.ReadOnly);
        try
        {
            Assert.Equal(-6, await RunAsync("importall", _projectFile, _imageDirectory));
            Assert.Contains("Arranger is read-only because it reads data file 'simple', which is read-only", Output);
        }
        finally
        {
            File.SetAttributes(SimpleDataFile, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task Import_ReadOnlyArranger_Exits6()
    {
        UsePluginEnvironment();

        Assert.Equal(-6, await RunAsync("import", _projectFile, _imageDirectory, FontKey));
        Assert.Contains("Arranger is read-only because it uses codec 'Last Armageddon Font' that cannot encode", Output);
    }

    #endregion

    #region Generate

    private const string TableDataKey = "/Data/table";

    private string DefinitionsDirectory => Path.Combine(_directory, "defs");
    private string DefinitionFile => Path.Combine(DefinitionsDirectory, "Synthetic.table.xml");

    private void UseTableData()
    {
        File.WriteAllBytes(Path.Combine(_directory, "Resources", "table.bin"), FireRedShapedData.Create());
        WriteResource("Data/table.xml", """<datafile location="table.bin" />""");

        Directory.CreateDirectory(DefinitionsDirectory);
        foreach (var file in Directory.EnumerateFiles(TableTestContext.FixturesDirectory, "*.xml").Where(x => !x.EndsWith(".manifest.xml")))
            File.Copy(file, Path.Combine(DefinitionsDirectory, Path.GetFileName(file)));

        UseShippedMapFormats();
    }

    private string[] ListFiles() => Directory.GetFiles(_directory, "*", SearchOption.AllDirectories)
        .Where(x => !x.StartsWith(Path.GetDirectoryName(_defaultLogFile)!, StringComparison.OrdinalIgnoreCase))
        .Order()
        .ToArray();

    [Fact]
    public async Task Generate_WritesResourcesExits0()
    {
        UseTableData();

        Assert.Equal(0, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated"));

        Assert.Contains("Created 1 compresseddata", Output);
        Assert.Contains("Created 13 palette", Output);
        Assert.Contains("Created 3 metatilemap", Output);
        Assert.Contains("Warning: Metatile set '/Generated/Tilesets/Viridian City' declared 1 metatiles, maps use up to 4, raised to 4", Output);
        Assert.DoesNotContain("metatilemap /Generated", Output);
        Assert.True(File.Exists(Path.Combine(_directory, "Resources", "Generated", "Maps", "Pallet Town", "Pallet Town.xml")));
        Assert.True(File.Exists(Path.Combine(DefinitionsDirectory, "Synthetic.manifest.xml")));
        Assert.Contains("Operation completed successfully", Output);

        Assert.Equal(0, await RunAsync("exportall", _projectFile, _imageDirectory));
        Assert.True(File.Exists(ImagePath("Generated/Maps/Pallet Town/Pallet Town")));
    }

    private string WriteLegacyCodecArranger()
    {
        WriteResource("Graphics/Legacy.xml", """
            <arranger elementsx="1" elementsy="1" width="8" height="8" layout="tiled" color="indexed" defaultcodec="SNES 3bpp Flow" defaultdatafile="/Data/simple" defaultpalette="/Palettes/Simple">
            	<element fileoffset="0" posx="0" posy="0" />
            </arranger>
            """);
        return Path.Combine(_directory, "Resources", "Graphics", "Legacy.xml");
    }

    [Fact]
    public async Task Generate_LeavesOtherResourcesUnwritten()
    {
        UseTableData();
        var legacy = WriteLegacyCodecArranger();

        Assert.Equal(0, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated"));

        Assert.Contains("SNES 3bpp Flow", File.ReadAllText(legacy));
    }

    [Fact]
    public async Task Generate_SaveProject_WritesOtherChangedResources()
    {
        UseTableData();
        var legacy = WriteLegacyCodecArranger();

        Assert.Equal(0, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated", "--save-project"));

        Assert.DoesNotContain("SNES 3bpp Flow", File.ReadAllText(legacy));
        Assert.Contains("defaultcodec=\"SNES 3bpp\"", File.ReadAllText(legacy));
    }

    [Fact]
    public async Task Generate_DryRun_WritesNothingExits0()
    {
        UseTableData();
        var before = ListFiles();

        Assert.Equal(0, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated", "--dry-run"));

        Assert.Contains("metatilemap /Generated/Maps/Pallet Town/Pallet Town (3x2 blocks)", Output);
        Assert.Contains("palette /Generated/Tilesets/Palettes/General 0 (16 colors)", Output);
        Assert.Contains("Would create 3 metatileset", Output);
        Assert.Contains("Warning: Metatile set '/Generated/Tilesets/Viridian City'", Output);
        Assert.Equal(before, ListFiles());
        Assert.False(Directory.Exists(Path.Combine(_directory, "Resources", "Generated")));
    }

    [Fact]
    public async Task Generate_InvalidDefinition_Exits8()
    {
        UseTableData();
        File.WriteAllText(DefinitionFile, File.ReadAllText(DefinitionFile).Replace("offset=\"metatiles\"", "offset=\"metatiles +\""));

        Assert.Equal(-8, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated"));

        Assert.Contains($"Definition '{DefinitionFile}' contained 1 errors", Output);
        Assert.Contains("1: ", Output);
        Assert.Contains("output 'tilesetSet' attribute 'offset'", Output);
        Assert.Contains("Operation failed due to a generation error", Output);
    }

    [Fact]
    public async Task Generate_UnknownDataFileKey_Exits8()
    {
        UseTableData();
        var before = ListFiles();

        Assert.Equal(-8, await RunAsync("generate", _projectFile, "/Data/nope", DefinitionFile, "/Generated"));

        Assert.Contains("'/Data/nope' is not a data file", Output);
        Assert.Equal(before, ListFiles());
    }

    [Fact]
    public async Task PascalCaseGenerate_Parses()
    {
        UseTableData();

        Assert.Equal(0, await RunAsync("Generate", _projectFile, TableDataKey, DefinitionFile, "/Generated", "--dry-run"));
    }


    private string TableFile => Path.Combine(_directory, "Resources", "table.bin");
    private string GeneratedMapFile => Path.Combine(_directory, "Resources", "Generated", "Maps", "Pallet Town", "Pallet Town.xml");
    private string RemovedPaletteFile => Path.Combine(_directory, "Resources", "Generated", "Tilesets", "Palettes", "Pallet Town 12.xml");

    private async Task GenerateTableDataAsync()
    {
        UseTableData();
        Assert.Equal(0, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated"));
        _output.GetStringBuilder().Clear();
    }

    private void RepointTableLayout()
    {
        var rom = File.ReadAllBytes(TableFile);
        FireRedShapedData.RepointLayoutBlocks(rom, FireRedShapedData.LayoutA, FireRedShapedData.FreeSpace);
        File.WriteAllBytes(TableFile, rom);
    }

    private void DropLastSecondaryPalette() =>
        File.WriteAllText(DefinitionFile, File.ReadAllText(DefinitionFile).Replace("repeat=\"isSecondary ? 6 : 7\"", "repeat=\"isSecondary ? 5 : 7\""));

    private Dictionary<string, byte[]> ReadAllFiles() => ListFiles().ToDictionary(x => x, File.ReadAllBytes);

    private void AssertFilesUnchanged(Dictionary<string, byte[]> before)
    {
        var after = ReadAllFiles();
        Assert.Equal(before.Keys, after.Keys);
        foreach (var (file, bytes) in before)
            Assert.Equal(bytes, after[file]);
    }

    [Fact]
    public async Task Regenerate_PrintsChangesExits0()
    {
        await GenerateTableDataAsync();
        RepointTableLayout();
        var mapBytes = File.ReadAllBytes(GeneratedMapFile);

        Assert.Equal(0, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated"));

        Assert.Contains("Changed 1 metatilemap", Output);
        Assert.Contains("Unchanged 2 metatilemap", Output);
        Assert.Contains("Unchanged 13 palette", Output);
        Assert.Contains("Changed /Generated/Maps/Pallet Town/Pallet Town: repointed", Output);
        Assert.DoesNotContain("Created", Output);
        Assert.NotEqual(mapBytes, File.ReadAllBytes(GeneratedMapFile));
    }

    [Fact]
    public async Task Regenerate_DryRun_WritesNothingExits0()
    {
        await GenerateTableDataAsync();
        RepointTableLayout();
        var before = ReadAllFiles();

        Assert.Equal(0, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated", "--dry-run"));

        Assert.Contains("Would change 1 metatilemap", Output);
        Assert.Contains("Would change /Generated/Maps/Pallet Town/Pallet Town: repointed", Output);
        Assert.DoesNotContain("Changed", Output);
        AssertFilesUnchanged(before);
    }

    [Fact]
    public async Task Regenerate_RemovalsWithoutYes_Exits8()
    {
        await GenerateTableDataAsync();
        DropLastSecondaryPalette();
        var before = ReadAllFiles();

        Assert.Equal(-8, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated"));

        Assert.Contains("Regeneration would remove 1 resources; rerun with --yes to remove them", Output);
        Assert.Contains("/Generated/Tilesets/Palettes/Pallet Town 12", Output);
        Assert.Contains("Operation failed due to a generation error", Output);
        AssertFilesUnchanged(before);
    }

    [Fact]
    public async Task Regenerate_RemovalsWithYes_Exits0()
    {
        await GenerateTableDataAsync();
        DropLastSecondaryPalette();
        Assert.True(File.Exists(RemovedPaletteFile));

        Assert.Equal(0, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated", "--yes"));

        Assert.Contains("Removed 1 palette", Output);
        Assert.Contains("Removed /Generated/Tilesets/Palettes/Pallet Town 12", Output);
        Assert.Contains("Changed /Generated/Tilesets/Pallet Town: re-paletted", Output);
        Assert.False(File.Exists(RemovedPaletteFile));
    }

    [Fact]
    public async Task Regenerate_OverwriteEdited_RewritesEditedResource()
    {
        await GenerateTableDataAsync();
        File.WriteAllText(GeneratedMapFile, File.ReadAllText(GeneratedMapFile).Replace("<metatilemap", "<!-- edited -->\n<metatilemap"));
        RepointTableLayout();

        Assert.Equal(0, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated"));

        Assert.Contains("Kept edited /Generated/Maps/Pallet Town/Pallet Town", Output);
        Assert.Contains("edited", File.ReadAllText(GeneratedMapFile));

        _output.GetStringBuilder().Clear();
        Assert.Equal(0, await RunAsync("generate", _projectFile, TableDataKey, DefinitionFile, "/Generated", "--overwrite-edited"));

        Assert.Contains("Changed /Generated/Maps/Pallet Town/Pallet Town: repointed", Output);
        Assert.DoesNotContain("edited", File.ReadAllText(GeneratedMapFile));
    }

    #endregion
}
