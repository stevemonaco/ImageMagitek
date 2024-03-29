﻿using ImageMagitek.Codec;
using McMaster.NETCore.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImageMagitek.Services;

public interface IPluginService
{
    public IDictionary<string, Type> CodecPlugins { get; }

    void LoadCodecPlugins(string pluginsPath);
}

public sealed class PluginService : IPluginService
{
    public IDictionary<string, Type> CodecPlugins { get; } = new Dictionary<string, Type>();

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

                var codecs = pluginTypes.Select(Activator.CreateInstance)
                    .OfType<IGraphicsCodec>();

                foreach (var codec in codecs)
                    CodecPlugins.Add(codec.Name, codec.GetType());
            }
        }
    }
}
