using ImageMagitek.Codec;
using ImageMagitek.Plugins;
using McMaster.NETCore.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ImageMagitek.Services;

public interface IPluginService
{
    /// <summary>
    /// Discovered plugin codec types, which are validated and named by <see cref="ICodecFactory.AddCodecPlugin"/>.
    /// </summary>
    public IList<Type> CodecPlugins { get; }

    /// <summary>
    /// Names of the plugin codecs that were registered with the codec factory.
    /// </summary>
    public IList<string> CodecNames { get; }

    /// <summary>
    /// Loads <c>&lt;sub&gt;/&lt;sub&gt;.dll</c> from each subdirectory and collects its codec types
    /// </summary>
    /// <returns>A failure naming each plugin that could not be loaded; the other plugins' types are still collected</returns>
    MagitekResults LoadCodecPlugins(string pluginsPath);
}

public sealed class PluginService : IPluginService
{
    private const string ContractsAssemblyName = "ImageMagitek.Plugins.Contracts";
    private const string LegacyCoreAssemblyName = "ImageMagitek";

    public IList<Type> CodecPlugins { get; } = new List<Type>();
    public IList<string> CodecNames { get; } = new List<string>();

    public MagitekResults LoadCodecPlugins(string pluginsPath)
    {
        var errors = new List<string>();
        var hostVersion = typeof(ICodecPlugin).Assembly.GetName().Version!;

        foreach (var dir in Directory.GetDirectories(pluginsPath))
        {
            var dirName = Path.GetFileName(dir);
            var pluginDll = Path.Combine(dir, dirName + ".dll");
            if (!File.Exists(pluginDll))
                continue;

            try
            {
                var codecLoader = PluginLoader.CreateFromAssemblyFile(
                    pluginDll,
                    sharedTypes: [typeof(ICodecPlugin)]);

                var assembly = codecLoader.LoadDefaultAssembly();
                var references = assembly.GetReferencedAssemblies();

                // Checked before GetTypes, which would fail to bind a newer contract with an unclear error
                if (references.FirstOrDefault(x => x.Name == ContractsAssemblyName)?.Version is { } referenced
                    && CheckContractVersion(referenced, hostVersion) is { } incompatible)
                {
                    errors.Add($"Plugin '{pluginDll}' was skipped because it was {incompatible}");
                    continue;
                }

                var referencesLegacyCore = references.Any(x => x.Name == LegacyCoreAssemblyName);
                List<Type> pluginTypes;
                try
                {
                    pluginTypes = FindPluginTypes(assembly.GetTypes());
                }
                // Pre-1.0 codecs derive from core types that no longer exist, so they fail to load here
                catch (ReflectionTypeLoadException ex) when (referencesLegacyCore && FindPluginTypes(ex.Types).Count == 0)
                {
                    pluginTypes = [];
                }

                if (pluginTypes.Count == 0 && referencesLegacyCore)
                {
                    errors.Add($"Plugin '{pluginDll}' was built for the pre-1.0 plugin contract and must be rebuilt against {ContractsAssemblyName}");
                    continue;
                }

                foreach (var pluginType in pluginTypes)
                    CodecPlugins.Add(pluginType);
            }
            catch (Exception ex)
            {
                errors.Add($"Plugin '{pluginDll}' could not be loaded: {ex.Message}");
            }
        }

        if (errors.Count > 0)
            return new MagitekResults.Failed(errors);

        return MagitekResults.SuccessResults;
    }

    private static List<Type> FindPluginTypes(IEnumerable<Type?> types) =>
        types.OfType<Type>()
            .Where(t => typeof(ICodecPlugin).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

    /// <summary>
    /// Checks the plugin contract version a plugin references against the host's
    /// </summary>
    /// <returns>Null when compatible, otherwise why the plugin cannot load, naming both versions</returns>
    public static string? CheckContractVersion(Version referenced, Version host)
    {
        if (referenced.Major != host.Major)
            return $"built for an incompatible plugin contract ({referenced.Major}.{referenced.Minor}; this TileShop has {host.Major}.{host.Minor})";

        if (referenced.Minor > host.Minor)
            return $"built for a newer TileShop (plugin contract {referenced.Major}.{referenced.Minor}; this TileShop has {host.Major}.{host.Minor})";

        return null;
    }
}
