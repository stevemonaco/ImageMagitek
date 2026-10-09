using System;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.PluginSample;
using ImageMagitek.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ImageMagitek.UnitTests.Services;

public sealed class BootstrapServiceTests : IDisposable
{
    private static readonly BootstrapPaths _shipped = BootstrapPaths.FromDirectory(AppContext.BaseDirectory);

    private readonly string _directory = TestPaths.CreateTempPath("");
    private readonly BootstrapService _bootstrapper = new(NullLogger.Instance);
    private readonly IPaletteService _paletteService = new PaletteService(new ImageMagitek.Colors.ColorFactory());

    public BootstrapServiceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A plugin DLL loaded into its own context stays locked
        }
    }

    [Fact]
    public void ReadConfiguration_MalformedFile_ReturnsDefaultsAndRecordsIssue()
    {
        var settingsFileName = Path.Combine(_directory, "appsettings.json");
        File.WriteAllText(settingsFileName, "{ not json");

        var settings = _bootstrapper.ReadConfiguration(new SettingsService(), settingsFileName);

        Assert.Equal(SettingsService.CreateDefault().NesPalette, settings.NesPalette);
        var issue = Assert.Single(_bootstrapper.Issues);
        Assert.Equal(settingsFileName, issue.Path);
    }

    [Fact]
    public void CreatePaletteStore_MissingGlobalPalette_SkipsAndRecordsIssue()
    {
        var palettesPath = CreateShippedPalettes();
        var settings = SettingsService.CreateDefault() with { GlobalPalettes = ["DefaultRgba32", "Missing"] };

        var store = _bootstrapper.CreatePaletteStore(_paletteService, palettesPath, settings);

        Assert.Single(store.GlobalPalettes);
        var issue = Assert.Single(_bootstrapper.Issues);
        Assert.Equal(Path.Combine(palettesPath, "Missing.json"), issue.Path);
    }

    [Fact]
    public void CreatePaletteStore_MalformedJson_SkipsAndRecordsIssue()
    {
        var palettesPath = CreateShippedPalettes();
        File.WriteAllText(Path.Combine(palettesPath, "Broken.json"), "{ \"colors\": [");
        var settings = SettingsService.CreateDefault() with { GlobalPalettes = ["Broken", "DefaultRgba32"] };

        var store = _bootstrapper.CreatePaletteStore(_paletteService, palettesPath, settings);

        Assert.Equal("DefaultRgba32", store.DefaultPalette.Name);
        var issue = Assert.Single(_bootstrapper.Issues);
        Assert.Equal(Path.Combine(palettesPath, "Broken.json"), issue.Path);
    }

    [Fact]
    public void CreatePaletteStore_NoGlobalPalette_ThrowsBootstrapException()
    {
        var palettesPath = CreateShippedPalettes();
        var settings = SettingsService.CreateDefault() with { GlobalPalettes = ["Missing"] };

        var ex = Assert.Throws<BootstrapException>(() => _bootstrapper.CreatePaletteStore(_paletteService, palettesPath, settings));

        Assert.Contains(palettesPath, ex.Message);
    }

    [Fact]
    public void CreatePaletteStore_ShortNesPalette_ThrowsBootstrapException()
    {
        var palettesPath = CreateShippedPalettes();
        WriteShortPalette(palettesPath, "ShortNes");
        var settings = SettingsService.CreateDefault() with { NesPalette = "ShortNes" };

        var ex = Assert.Throws<BootstrapException>(() => _bootstrapper.CreatePaletteStore(_paletteService, palettesPath, settings));

        Assert.Contains("'ShortNes'", ex.Message);
    }

    [Fact]
    public void CreatePaletteStore_UnusableOverride_FallsBackAndRecordsIssue()
    {
        var palettesPath = CreateShippedPalettes();
        WriteShortPalette(palettesPath, "ShortNes");

        var store = _bootstrapper.CreatePaletteStore(_paletteService, palettesPath, SettingsService.CreateDefault(), "ShortNes");

        Assert.Equal("DefaultNes", store.NesPalette?.Name);
        var issue = Assert.Single(_bootstrapper.Issues);
        Assert.Equal(Path.Combine(palettesPath, "ShortNes.json"), issue.Path);
    }

    [Fact]
    public void CreateCodecService_MissingFolder_RecordsIssue()
    {
        var codecsPath = Path.Combine(_directory, "_codecs");
        var factory = new CodecFactory(TestImageGenerator.CreateDistinctPalette(2), []);

        var service = _bootstrapper.CreateCodecService(codecsPath, _shipped.CodecSchemaFileName, factory);

        Assert.Contains("PSX 16bpp", service.GetSupportedCodecNames());
        var issue = Assert.Single(_bootstrapper.Issues);
        Assert.Equal(codecsPath, issue.Path);
    }

    [Fact]
    public void CreateCodecService_MissingSchema_ThrowsBootstrapException()
    {
        var schemaFileName = Path.Combine(_directory, "CodecSchema.xsd");
        var factory = new CodecFactory(TestImageGenerator.CreateDistinctPalette(2), []);

        var ex = Assert.Throws<BootstrapException>(() => _bootstrapper.CreateCodecService(TestPaths.CodecsPath, schemaFileName, factory));

        Assert.Contains(schemaFileName, ex.Message);
    }

    [Fact]
    public void CreatePluginService_CollidingName_SkipsAndRecordsIssue()
    {
        var samplesAssembly = typeof(Snes3BppCodec).Assembly;
        var pluginName = samplesAssembly.GetName().Name!;
        var pluginDirectory = Directory.CreateDirectory(Path.Combine(_directory, pluginName));
        File.Copy(samplesAssembly.Location, Path.Combine(pluginDirectory.FullName, pluginName + ".dll"));

        var factory = new CodecFactory(TestImageGenerator.CreateDistinctPalette(8), []);
        Assert.True(factory.AddCodec(typeof(Snes3BppCodec)).HasSucceeded);
        var codecService = new XmlCodecService(_shipped.CodecSchemaFileName, factory);

        var pluginService = _bootstrapper.CreatePluginService(_directory, codecService);

        var issue = Assert.Single(_bootstrapper.Issues);
        Assert.Contains("SNES 3bpp Plugin", issue.Message);
        Assert.DoesNotContain(pluginService.CodecPlugins, x => x.FullName == typeof(Snes3BppCodec).FullName);
        Assert.Contains(pluginService.CodecPlugins, x => x.FullName == typeof(Snes4BppCodec).FullName);
        Assert.Contains("SNES 4bpp Plugin", factory.GetRegisteredCodecNames());
    }

    [Fact]
    public void CreateElementStore_DuplicateName_KeepsFirstAndRecordsIssue()
    {
        var layoutsPath = Directory.CreateDirectory(Path.Combine(_directory, "_layouts")).FullName;
        File.WriteAllText(Path.Combine(layoutsPath, "a.json"), LayoutJson("Same", 1));
        File.WriteAllText(Path.Combine(layoutsPath, "b.json"), LayoutJson("Same", 2));

        var store = _bootstrapper.CreateElementStore(new ElementLayoutService(), layoutsPath);

        Assert.Equal(1, store.ElementLayouts["Same"].Width);
        var issue = Assert.Single(_bootstrapper.Issues);
        Assert.Equal(Path.Combine(layoutsPath, "b.json"), issue.Path);
    }

    private string CreateShippedPalettes()
    {
        var palettesPath = Directory.CreateDirectory(Path.Combine(_directory, "_palettes")).FullName;
        foreach (var file in Directory.GetFiles(_shipped.PalettesPath, "*.json"))
            File.Copy(file, Path.Combine(palettesPath, Path.GetFileName(file)));

        return palettesPath;
    }

    private static void WriteShortPalette(string palettesPath, string name)
    {
        var colors = string.Join(", ", Enumerable.Repeat("\"#000000FF\"", 4));
        File.WriteAllText(Path.Combine(palettesPath, $"{name}.json"),
            $$"""{ "name": "{{name}}", "zeroindextransparent": false, "colors": [ {{colors}} ] }""");
    }

    private static string LayoutJson(string name, int width) =>
        $$"""
        { "name": "{{name}}", "width": {{width}}, "height": 1, "tilesPerPattern": 1, "pattern": [ { "x": 0, "y": 0 } ] }
        """;
}
