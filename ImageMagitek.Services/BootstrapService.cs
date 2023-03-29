using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services.Stores;
using Microsoft.Extensions.Logging;

namespace ImageMagitek.Services;

/// <summary>
/// Bootstraps the full ImageMagitek environment 
/// </summary>
public class BootstrapService
{
    private readonly ILogger _logger;

    public static string DefaultLogFileName { get; } = "errorlog.txt";
    public static string DefaultConfigurationFileName { get; } = "appsettings.json";
    public static string DefaultPalettePath { get; } = "_palettes";
    public static string DefaultCodecPath { get; } = "_codecs";
    public static string DefaultPluginPath { get; } = "_plugins";
    public static string DefaultLayoutsPath { get; } = "_layouts";
    public static string DefaultResourceSchemaFileName { get; } = Path.Combine("_schemas", "ResourceSchema.xsd");
    public static string DefaultCodecSchemaFileName { get; } = Path.Combine("_schemas", "CodecSchema.xsd");

    public BootstrapService(ILogger logger)
    {
        _logger = logger;
    }

    public virtual SettingsService CreateSettingsService() => new SettingsService();

    public virtual async Task<AppSettings?> ReadConfiguration(SettingsService settingsService, string jsonFileName)
    {
        try
        {
            var settings = await settingsService.ReadSettings(jsonFileName);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, $"Failed to read the configuration file '{jsonFileName}'");
            throw;
        }
    }

    public virtual PaletteStore CreatePaletteStore(IPaletteService paletteService, string palettesPath, AppSettings settings)
    {
        var globalPalettes = new List<Palette>();
        foreach (var paletteName in settings.GlobalPalettes)
        {
            var paletteFileName = Path.Combine(palettesPath, $"{paletteName}.json");
            var palette = paletteService.ReadJsonPalette(paletteFileName);

            if (palette is not null)
                globalPalettes.Add(palette);
            else
                _logger.LogError($"Could not load default palette named '{paletteName}' at expected location '{paletteFileName}'");
        }

        var nesPaletteFileName = Path.Combine(palettesPath, $"{settings.NesPalette}.json");
        var nesPalette = paletteService.ReadJsonPalette(nesPaletteFileName);
        var defaultPalette = globalPalettes.First();

        if (nesPalette is null)
        {
            _logger.LogError($"Could not load NES palette named '{settings.NesPalette}' at expected location '{nesPaletteFileName}'");
            return new PaletteStore(defaultPalette, globalPalettes);
        }

        return new PaletteStore(defaultPalette, nesPalette, globalPalettes);
    }

    public virtual IPaletteService CreatePaletteService(IColorFactory colorFactory)
    {
        return new PaletteService(colorFactory);
    }

    public virtual IColorFactory CreateColorFactory()
    {
        var factory = new ColorFactory();

        return factory;
    }

    public virtual ICodecService CreateCodecService(string codecsPath, string schemaFileName, CodecFactory codecFactory)
    {
        var codecService = new XmlCodecService(schemaFileName, codecFactory);
        var result = codecService.LoadCodecs(codecsPath);

        if (result.Value is MagitekResults.Failed fail)
        {
            _logger.LogError(string.Join(Environment.NewLine, fail.Reasons));
        }

        return codecService;
    }

    public virtual IPluginService CreatePluginService(string pluginPath, ICodecService codecService)
    {
        var pluginService = new PluginService();
        var fullPluginPath = Path.GetFullPath(pluginPath);

        if (Directory.Exists(fullPluginPath))
        {
            pluginService.LoadCodecPlugins(fullPluginPath);
            foreach (var codecPlugin in pluginService.CodecPlugins)
            {
                codecService.AddOrUpdateCodec(codecPlugin.Value);
            }
        }

        return pluginService;
    }

    public virtual IProjectService CreateProjectService(IProjectSerializerFactory serializerFactory, IColorFactory colorFactory)
    {
        var projectService = new ProjectService(serializerFactory, colorFactory);

        return projectService;
    }

    public virtual IElementLayoutService CreateElementLayoutService()
    {
        return new ElementLayoutService();
    }

    public virtual ElementStore CreateElementStore(IElementLayoutService layoutService, string layoutPath)
    {
        var store = new ElementStore();

        if (Directory.Exists(layoutPath))
        {
            foreach (var fileName in Directory.GetFiles(layoutPath, "*.json"))
            {
                var result = layoutService.ReadLayout(fileName);

                result.Switch(
                    success => store.ElementLayouts.Add(success.Result.Name, success.Result),
                    fail => _logger.LogWarning($"Could not read layout '{fileName}': {fail.Reason}")
                );
            }
        }

        return store;
    }
}
