using System;
using System.IO;
using System.Text.Json;
using ImageMagitek.Services;
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
        var shipped = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, BootstrapService.DefaultConfigurationFileName));

        var settings = _service.Deserialize(shipped);
        var defaults = SettingsService.CreateDefault();

        Assert.Equal(defaults.NesPalette, settings.NesPalette);
        Assert.Equal(defaults.GlobalPalettes, settings.GlobalPalettes);
        Assert.Equal(defaults.ExtensionCodecAssociations, settings.ExtensionCodecAssociations);
    }
}
