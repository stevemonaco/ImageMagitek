using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ImageMagitek.Services;
using ImageMagitek.UnitTests.Fixtures;
using Xunit;

namespace ImageMagitek.UnitTests;

public class SettingsServiceTests
{
    private readonly SettingsService _service = new();

    [Fact]
    public void Deserialize_CamelCaseKeys_MapsToRecord()
    {
        var json = """
            {
              "extensionCodecAssociations": { "default": "NES 1bpp", ".sfc": "SNES 4bpp" },
              "globalPalettes": [ "DefaultRgba32", "Custom" ],
              "nesPalette": "MyNes"
            }
            """;

        var settings = _service.Deserialize(json);

        Assert.Equal("SNES 4bpp", settings.ExtensionCodecAssociations[".sfc"]);
        Assert.Equal(["DefaultRgba32", "Custom"], settings.GlobalPalettes);
        Assert.Equal("MyNes", settings.NesPalette);
    }

    [Fact]
    public void Deserialize_CommentsAndTrailingCommas_AreTolerated()
    {
        var json = """
            {
              // user note
              "extensionCodecAssociations": { "default": "NES 1bpp", },
              "globalPalettes": [ "DefaultRgba32", ],
              "nesPalette": "DefaultNes",
            }
            """;

        var settings = _service.Deserialize(json);

        Assert.Equal("NES 1bpp", settings.ExtensionCodecAssociations["default"]);
    }

    [Fact]
    public void Deserialize_MalformedJson_Throws()
    {
        Assert.Throws<JsonException>(() => _service.Deserialize("{ not json"));
    }

    [Fact]
    public void Deserialize_PartialFile_MissingKeysTakeDefaults()
    {
        var settings = _service.Deserialize("""{ "nesPalette": "MyNes" }""");
        var defaults = SettingsService.CreateDefault();

        Assert.Equal("MyNes", settings.NesPalette);
        Assert.Equal(defaults.GlobalPalettes, settings.GlobalPalettes);
        Assert.Equal(defaults.ExtensionCodecAssociations, settings.ExtensionCodecAssociations);
    }

    [Fact]
    public void Deserialize_EmptyGlobalPalettes_TakesDefault()
    {
        var settings = _service.Deserialize("""{ "globalPalettes": [] }""");

        Assert.Equal(SettingsService.CreateDefault().GlobalPalettes, settings.GlobalPalettes);
    }

    [Fact]
    public void Deserialize_ExtensionAssociations_ReplaceDefaults()
    {
        var settings = _service.Deserialize("""{ "extensionCodecAssociations": { ".sfc": "SNES 4bpp" } }""");

        var association = Assert.Single(settings.ExtensionCodecAssociations);
        Assert.Equal(".sfc", association.Key);
    }

    [Fact]
    public void ReadSettings_MissingFile_ReturnsDefaults()
    {
        var missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var settings = _service.ReadSettings(missing);

        Assert.Equal("DefaultNes", settings.NesPalette);
        Assert.True(settings.ExtensionCodecAssociations.ContainsKey("default"));
    }

    [Fact]
    public void ShippedAppSettings_MatchesCodeDefaults()
    {
        var shipped = File.ReadAllText(BootstrapPaths.FromDirectory(AppContext.BaseDirectory).SettingsFileName);

        var settings = _service.Deserialize(shipped);
        var defaults = SettingsService.CreateDefault();

        Assert.Equal(defaults.NesPalette, settings.NesPalette);
        Assert.Equal(defaults.GlobalPalettes, settings.GlobalPalettes);
        Assert.Equal(defaults.ExtensionCodecAssociations, settings.ExtensionCodecAssociations);
    }

    [Fact]
    public void Defaults_AssociationsNameRegisteredCodecs()
    {
        var registered = CodecFixture.Shared.CodecFactory.GetRegisteredCodecNames().ToHashSet();
        var shipped = _service.Deserialize(File.ReadAllText(BootstrapPaths.FromDirectory(AppContext.BaseDirectory).SettingsFileName));

        var associations = SettingsService.CreateDefault().ExtensionCodecAssociations.Concat(shipped.ExtensionCodecAssociations);

        Assert.All(associations, x => Assert.Contains(x.Value, registered));
    }
}
