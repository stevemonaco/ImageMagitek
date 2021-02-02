using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ImageMagitek.Colors;
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
        public static string DefaultProjectSchemaFileName { get; } = Path.Combine("_schemas", "GameDescriptorSchema.xsd");
        public static string DefaultCodecSchemaFileName { get; } = Path.Combine("_schemas", "CodecSchema.xsd");

        public BootstrapService(ILogger logger)
        {
            _logger = logger;
        }

        public AppSettings ReadConfiguration(string jsonFileName)
        {
            try
            {
                var json = File.ReadAllText(jsonFileName);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<AppSettings>(json, options);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"Failed to read the configuration file '{jsonFileName}'");
                throw;
            }
        }

        public IPaletteService CreatePaletteService(string palettesPath, AppSettings settings)
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

        public ICodecService CreateCodecService(string codecsPath, string schemaFileName)
        {
            var _codecService = new CodecService(schemaFileName);
            var result = _codecService.LoadXmlCodecs(codecsPath);

            if (result.Value is MagitekResults.Failed fail)
            {
                _logger.LogError(string.Join(Environment.NewLine, fail.Reasons));
            }

            return _codecService;
        }

        public IPluginService CreatePluginService(string pluginPath, ICodecService codecService)
        {
            var pluginService = new PluginService();
            var fullPath = Path.GetFullPath(pluginPath);
            pluginService.LoadCodecPlugins(fullPath);
            foreach (var codecPlugin in pluginService.CodecPlugins)
            {
                codecService.AddOrUpdateCodec(codecPlugin.Value);
            }

            return pluginService;
        }

        public IProjectService CreateProjectService(string schemaFileName, IPaletteService paletteService, ICodecService codecService)
        {
            var defaultResources = paletteService.GlobalPalettes;
            var projectService = new ProjectService(codecService, paletteService.ColorFactory, defaultResources);
            projectService.LoadSchemaDefinition(schemaFileName);

            return projectService;
        }
    }
}
