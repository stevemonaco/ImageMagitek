using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using ImageMagitek.Colors;

namespace ImageMagitek.Services;
public sealed class SettingsService
{
    public AppSettings? Deserialize(string jsonContent)
    {
        Guard.IsNotNullOrEmpty(jsonContent);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var settings = JsonSerializer.Deserialize<AppSettings>(jsonContent, options);
        return settings;
    }

    public string Serialize(AppSettings settings)
    {
        return JsonSerializer.Serialize(settings);
    }

    /// <summary>
    /// Reads settings from the specified file
    /// </summary>
    /// <param name="fileLocation">Location on disk to read the file</param>
    /// <returns>null if the file cannot be serialized</returns>
    public async Task<AppSettings?> ReadSettings(string fileLocation)
    {
        Guard.IsNotNullOrEmpty(fileLocation);

        if (!File.Exists(fileLocation))
            return CreateDefault();

        var content = await File.ReadAllTextAsync(fileLocation);
        var settings = Deserialize(content);

        return settings;
    }

    /// <summary>
    /// Reads settings from the specified file or provides a default if the file cannot be found or serialized
    /// </summary>
    /// <param name="fileLocation">Location on disk to read the file</param>
    /// <returns></returns>
    public async Task<AppSettings> ReadSettingsOrDefault(string fileLocation)
    {
        Guard.IsNotNullOrEmpty(fileLocation);

        if (!File.Exists(fileLocation))
            return CreateDefault();

        var content = await File.ReadAllTextAsync(fileLocation);
        var settings = Deserialize(content);

        return settings ?? CreateDefault();
    }

    /// <summary>
    /// Write settings to disk
    /// </summary>
    public async Task WriteSettings(string fileLocation, AppSettings settings)
    {
        Guard.IsNotNullOrEmpty(fileLocation);

        var content = Serialize(settings);
        await File.WriteAllTextAsync(fileLocation, content);
    }

    private static AppSettings CreateDefault()
    {
        return new AppSettings
        (
            GlobalPalettes: new[] { "DefaultRgba32" },
            NesPalette: "DefaultNes",
            EnableArrangerSymmetryTools: false,
            GridLineColor: new ColorRgba32(204, 132, 132, 196),
            PrimaryGridBackgroundColor: new ColorRgba32(0, 0, 0, 0),
            SecondaryGridBackgroundColor: new ColorRgba32(128, 128, 128, 25),

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
