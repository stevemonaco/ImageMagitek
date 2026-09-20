using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Diagnostics;

namespace ImageMagitek.Services;

public sealed class SettingsService
{
    /// <summary>
    /// Reads settings from the specified file or returns the built-in defaults when the file does not exist
    /// </summary>
    /// <exception cref="JsonException">The file exists but is not valid settings JSON</exception>
    public AppSettings ReadSettings(string fileLocation)
    {
        Guard.IsNotNullOrEmpty(fileLocation);

        if (!File.Exists(fileLocation))
            return CreateDefault();

        return Deserialize(File.ReadAllText(fileLocation));
    }

    public AppSettings Deserialize(string jsonContent)
    {
        Guard.IsNotNullOrEmpty(jsonContent);

        return JsonSerializer.Deserialize(jsonContent, AppSettingsJsonContext.Default.AppSettings)
            ?? throw new JsonException("Settings JSON deserialized to null");
    }

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        (
            GlobalPalettes: ["DefaultRgba32"],
            NesPalette: "DefaultNes",
            ExtensionCodecAssociations: new Dictionary<string, string>()
            {
                { "default", "NES 1bpp" },
                { ".gb", "SNES 2bpp" },
                { ".gba", "GBA 4bpp" },
                { ".gbc", "SNES 2bpp" },
                { ".gen", "Genesis 4bpp" },
                { ".gg", "Game Gear 4bpp" },
                { ".md", "Genesis 4bpp" },
                { ".n64", "N64 Rgba32" },
                { ".ncgr", "GBA 4bpp" },
                { ".ncbr", "GBA 4bpp" },
                { ".ngc", "NeoGeo Pocket 2bpp" },
                { ".nes", "NES 2bpp" },
                { ".sfc", "SNES 2bpp" },
                { ".smc", "SNES 2bpp" },
                { ".smd", "Genesis 4bpp" },
                { ".tim", "PSX 4bpp" },
                { ".vb", "Virtual Boy 2bpp" }
            }
        );
    }
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;
