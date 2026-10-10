using System.IO;
using ImageMagitek.Codec;
using ImageMagitek.Services;
using Microsoft.Extensions.Logging;

namespace TileShop.CLI;

public static class CliBootstrap
{
    /// <summary>
    /// Builds the project service from the shipped resources and plugins, printing each startup issue to <paramref name="output"/>.
    /// </summary>
    /// <exception cref="BootstrapException">An essential resource could not be loaded</exception>
    public static IProjectService CreateProjectService(BootstrapPaths paths, ILoggerFactory loggerFactory, TextWriter output)
    {
        var bootstrapper = new BootstrapService(loggerFactory.CreateLogger<BootstrapService>());

        try
        {
            var settingsService = bootstrapper.CreateSettingsService();
            var settings = bootstrapper.ReadConfiguration(settingsService, paths.SettingsFileName);

            var colorFactory = bootstrapper.CreateColorFactory();
            var paletteService = bootstrapper.CreatePaletteService(colorFactory);
            var paletteStore = bootstrapper.CreatePaletteStore(paletteService, paths.PalettesPath, settings);

            if (paletteStore.NesPalette is not null)
                colorFactory.SetNesPalette(paletteStore.NesPalette);

            var codecFactory = new CodecFactory(paletteStore.DefaultPalette, new());
            var codecService = bootstrapper.CreateCodecService(paths.CodecsPath, paths.CodecSchemaFileName, codecFactory);
            bootstrapper.CreatePluginService(paths.PluginsPath, codecService);

            var serializerFactory = bootstrapper.CreateProjectSerializerFactory(paths.ResourceSchemaFileName,
                codecService.CodecFactory, colorFactory, paletteStore.GlobalPalettes);
            return bootstrapper.CreateProjectService(serializerFactory, colorFactory);
        }
        finally
        {
            foreach (var issue in bootstrapper.Issues)
                output.WriteLine(issue.Message);
        }
    }
}
