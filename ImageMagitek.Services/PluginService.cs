using ImageMagitek.Codec;
using McMaster.NETCore.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImageMagitek.Services;

public interface IPluginService
{
    /// <summary>
    /// Discovered codec types, which are named and instantiated by <see cref="ICodecFactory.AddOrUpdateCodec"/>.
    /// </summary>
    public IList<Type> CodecPlugins { get; }

    void LoadCodecPlugins(string pluginsPath);
}

public sealed class PluginService : IPluginService
{
    public IList<Type> CodecPlugins { get; } = new List<Type>();

    public void LoadCodecPlugins(string pluginsPath)
    {
        var loaders = new List<PluginLoader>();

        foreach (var dir in Directory.GetDirectories(pluginsPath))
        {
            var dirName = Path.GetFileName(dir);
            var pluginDll = Path.Combine(dir, dirName + ".dll");
            if (File.Exists(pluginDll))
            {
                var codecLoader = PluginLoader.CreateFromAssemblyFile(
                    pluginDll,
                    sharedTypes: new[] { typeof(IGraphicsCodec) });

                var pluginTypes = codecLoader.LoadDefaultAssembly()
                    .GetTypes()
                    .Where(t => typeof(IGraphicsCodec).IsAssignableFrom(t) && !t.IsAbstract);

                foreach (var pluginType in pluginTypes)
                    CodecPlugins.Add(pluginType);
            }
        }
    }
}
