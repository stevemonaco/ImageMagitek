using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Services.Stores;
using Microsoft.Extensions.Logging;

namespace ImageMagitek.Services;

/// <summary>
/// Bootstraps the full ImageMagitek environment. Bad resource files are logged, skipped and recorded in <see cref="Issues"/>;
/// a missing essential resource throws <see cref="BootstrapException"/>.
/// </summary>
public class BootstrapService
{
    private const int NesPaletteMinimumEntries = 64;

    private readonly ILogger _logger;
    private readonly List<StartupIssue> _issues = [];

    /// <summary>
    /// Every resource file that was logged and skipped
    /// </summary>
    public IReadOnlyList<StartupIssue> Issues => _issues;

    public BootstrapService(ILogger logger)
    {
        _logger = logger;
    }

    public virtual SettingsService CreateSettingsService() => new SettingsService();

    public virtual AppSettings ReadConfiguration(SettingsService settingsService, string jsonFileName)
    {
        try
        {
            return settingsService.ReadSettings(jsonFileName);
        }
        catch (Exception ex)
        {
            RecordIssue(jsonFileName, $"The settings file could not be read, so the built-in defaults are used: {ex.Message}", ex);
            return SettingsService.CreateDefault();
        }
    }

    /// <param name="nesPaletteOverride">A palette name tried before the settings' NES palette, falling back when it is unusable</param>
    public virtual PaletteStore CreatePaletteStore(IPaletteService paletteService, string palettesPath, AppSettings settings, string? nesPaletteOverride = null)
    {
        var globalPalettes = new List<Palette>();
        foreach (var paletteName in settings.GlobalPalettes)
        {
            var paletteFileName = Path.Combine(palettesPath, $"{paletteName}.json");
            var palette = TryReadPalette(paletteService, paletteFileName, out var error);

            if (palette is not null)
                globalPalettes.Add(palette);
            else
                RecordIssue(paletteFileName, $"Global palette '{paletteName}' was not loaded: {error}");
        }

        if (globalPalettes.Count == 0)
            Fail($"No global palette could be loaded from '{palettesPath}'");

        Palette? nesPalette = null;
        if (nesPaletteOverride is not null)
        {
            var overrideFileName = Path.Combine(palettesPath, $"{nesPaletteOverride}.json");
            nesPalette = TryReadNesPalette(paletteService, overrideFileName, out var error);

            if (nesPalette is null)
                RecordIssue(overrideFileName, $"NES palette '{nesPaletteOverride}' from preferences was not used, so '{settings.NesPalette}' is used instead: {error}");
        }

        if (nesPalette is null)
        {
            var nesPaletteFileName = Path.Combine(palettesPath, $"{settings.NesPalette}.json");
            nesPalette = TryReadNesPalette(paletteService, nesPaletteFileName, out var error);

            if (nesPalette is null)
                Fail($"NES palette '{settings.NesPalette}' could not be loaded: {error}");
        }

        return new PaletteStore(globalPalettes[0], nesPalette, globalPalettes);
    }

    private static Palette? TryReadPalette(IPaletteService paletteService, string paletteFileName, out string? error)
    {
        try
        {
            var palette = paletteService.ReadJsonPalette(paletteFileName);
            error = palette is null ? "the file holds no palette" : null;
            return palette;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private static Palette? TryReadNesPalette(IPaletteService paletteService, string paletteFileName, out string? error)
    {
        var palette = TryReadPalette(paletteService, paletteFileName, out error);

        if (palette is not null && palette.Entries < NesPaletteMinimumEntries)
        {
            error = $"it has {palette.Entries} colors, but an NES palette needs at least {NesPaletteMinimumEntries}";
            return null;
        }

        return palette;
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
        if (!File.Exists(schemaFileName))
            Fail($"The codec schema '{schemaFileName}' does not exist");

        var codecService = new XmlCodecService(schemaFileName, codecFactory);

        if (!Directory.Exists(codecsPath))
        {
            RecordIssue(codecsPath, $"The codec folder '{codecsPath}' does not exist, so only built-in codecs are available");
            return codecService;
        }

        foreach (var failure in codecService.LoadCodecs(codecsPath))
            RecordIssue(failure.FileName, failure.Message);

        return codecService;
    }

    public virtual IPluginService CreatePluginService(string pluginPath, ICodecService codecService)
    {
        var pluginService = new PluginService();
        var fullPluginPath = Path.GetFullPath(pluginPath);

        if (!Directory.Exists(fullPluginPath))
            return pluginService;

        if (pluginService.LoadCodecPlugins(fullPluginPath).Value is MagitekResults.Failed fail)
        {
            foreach (var reason in fail.Reasons)
                RecordIssue(fullPluginPath, reason);
        }

        foreach (var codecType in pluginService.CodecPlugins.ToList())
        {
            var result = codecService.AddCodec(codecType);
            if (result.HasFailed)
            {
                RecordIssue(codecType.Assembly.Location, result.AsError.Reason);
                pluginService.CodecPlugins.Remove(codecType);
            }
        }

        return pluginService;
    }

    /// <summary>
    /// Creates the project serializer factory after checking that the resource schema exists
    /// </summary>
    public virtual IProjectSerializerFactory CreateProjectSerializerFactory(string resourceSchemaFileName, ICodecFactory codecFactory,
        IColorFactory colorFactory, IEnumerable<IProjectResource> globalResources)
    {
        if (!File.Exists(resourceSchemaFileName))
            Fail($"The project resource schema '{resourceSchemaFileName}' does not exist");

        return new XmlProjectSerializerFactory(resourceSchemaFileName, codecFactory, colorFactory, globalResources);
    }

    public virtual IProjectService CreateProjectService(IProjectSerializerFactory serializerFactory, IColorFactory colorFactory)
    {
        var projectService = new ProjectService(serializerFactory);

        return projectService;
    }

    public virtual IElementLayoutService CreateElementLayoutService()
    {
        return new ElementLayoutService();
    }

    public virtual ElementStore CreateElementStore(IElementLayoutService layoutService, string layoutPath)
    {
        var store = new ElementStore();

        if (!Directory.Exists(layoutPath))
        {
            RecordIssue(layoutPath, $"The layout folder '{layoutPath}' does not exist, so only the default layout is available");
            return store;
        }

        foreach (var fileName in Directory.GetFiles(layoutPath, "*.json").Order(StringComparer.Ordinal))
        {
            var result = layoutService.ReadLayout(fileName);

            result.Switch(
                success =>
                {
                    var layout = success.Result;
                    if (string.IsNullOrWhiteSpace(layout.Name))
                        RecordIssue(fileName, "Layout has no name and was skipped");
                    else if (!store.ElementLayouts.TryAdd(layout.Name, layout))
                        RecordIssue(fileName, $"Layout '{layout.Name}' duplicates a layout loaded from an earlier file and was skipped");
                },
                fail => RecordIssue(fileName, fail.Reason));
        }

        return store;
    }

    private void RecordIssue(string path, string message, Exception? exception = null)
    {
        _logger.LogWarning(exception, "{Message}", message);
        _issues.Add(new StartupIssue(path, message));
    }

    [DoesNotReturn]
    private void Fail(string message)
    {
        _logger.LogCritical("{Message}", message);
        throw new BootstrapException(message);
    }
}
