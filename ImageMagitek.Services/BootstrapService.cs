using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ImageMagitek.Colors;
using ImageMagitek.Project.Serialization;
using Microsoft.Extensions.Logging;

namespace ImageMagitek.Services
{
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
        public static string DefaultResourceSchemaFileName { get; } = Path.Combine("_schemas", "ResourceSchema.xsd");
        public static string DefaultCodecSchemaFileName { get; } = Path.Combine("_schemas", "CodecSchema.xsd");

        public BootstrapService(ILogger logger)
        {
            _logger = logger;
        }

        public virtual AppSettings ReadConfiguration(string jsonFileName)
        {
            try
            {
                var json = File.ReadAllText(jsonFileName);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var settings = JsonSerializer.Deserialize<AppSettings>(json, options);

                var lowerDict = new Dictionary<string, string>();
                foreach (var item in settings.ExtensionCodecAssociations)
                {
                    lowerDict.TryAdd(item.Key.ToLower(), item.Value);
                }

                settings.ExtensionCodecAssociations = lowerDict;

                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"Failed to read the configuration file '{jsonFileName}'");
                throw;
            }
        }

        public virtual IPaletteService CreatePaletteService(string palettesPath, AppSettings settings)
        {
            var _colorFactory = new ColorFactory();
            var _paletteService = new PaletteService(_colorFactory);

            foreach (var paletteName in settings.GlobalPalettes)
            {
                var paletteFileName = Path.Combine(palettesPath, $"{paletteName}.json");
                var palette = _paletteService.ReadJsonPalette(paletteFileName);
                _paletteService.GlobalPalettes.Add(palette);
            }
            _paletteService.SetDefaultPalette(_paletteService.GlobalPalettes.First());

            var nesPaletteFileName = Path.Combine(palettesPath, $"{settings.NesPalette}.json");
            var nesPalette = _paletteService.ReadJsonPalette(nesPaletteFileName);
            _colorFactory.SetNesPalette(nesPalette);
            _paletteService.SetNesPalette(nesPalette);

            return _paletteService;
        }

        public virtual ICodecService CreateCodecService(string codecsPath, string schemaFileName)
        {
            var _codecService = new CodecService(schemaFileName);
            var result = _codecService.LoadXmlCodecs(codecsPath);

            if (result.Value is MagitekResults.Failed fail)
            {
                _logger.LogError(string.Join(Environment.NewLine, fail.Reasons));
            }

            return _codecService;
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

        public virtual IProjectService CreateProjectService(IProjectSerializerFactory serializerFactory)
        {
            var projectService = new ProjectService(serializerFactory);

            return projectService;
        }
    }
}
