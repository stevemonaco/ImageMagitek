using System;
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
    private Func<ILoggerFactory, IProjectService> _bootstrap;

    public CliApplicationTests()
    {
        _directory = TestPaths.CreateTempPath("");
        Directory.CreateDirectory(_directory);
        _projectFile = ProjectFixtures.Create(ProjectFixtures.AllFeatures, _directory);
        _imageDirectory = Path.Combine(_directory, "images");
        _defaultLogFile = Path.Combine(_directory, "app", "errorlogCLI.txt");
        _bootstrap = _ => new ProjectService(ProjectFixtures.CreateSerializerFactory(_colorFactory));

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
        _bootstrap = loggerFactory => CliBootstrap.CreateProjectService(paths, loggerFactory, _output);
    }

    private sealed class YieldingProjectService(IProjectSerializerFactory serializerFactory) : ProjectService(serializerFactory)
    {
        public override async Task<MagitekResults<ProjectTree>> OpenProjectFileAsync(string projectFileName)
        {
            await Task.Yield();
            return await base.OpenProjectFileAsync(projectFileName);
        }
    }

    private sealed class ThrowingProjectService(IProjectSerializerFactory serializerFactory) : ProjectService(serializerFactory)
    {
        public override Task<MagitekResults<ProjectTree>> OpenProjectFileAsync(string projectFileName) =>
            throw new InvalidOperationException("Open failed on purpose");
    }

    #region Invocation and exit codes

    [Fact]
    public async Task OpenYields_ReturnsVerbCode()
    {
        _bootstrap = _ => new YieldingProjectService(ProjectFixtures.CreateSerializerFactory(_colorFactory));

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
        _bootstrap = loggerFactory => CliBootstrap.CreateProjectService(paths, loggerFactory, _output);

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
    public async Task OpenThrows_Exits2()
    {
        _bootstrap = _ => new ThrowingProjectService(ProjectFixtures.CreateSerializerFactory(_colorFactory));

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
        Assert.Contains("Resource key does not exist or is not a ScatteredArranger", Output);
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
    public async Task Export_ProjectUsingPluginCodec_Exits0()
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
}
