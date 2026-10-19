using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using ImageMagitek.Plugins;

namespace ImageMagitek.Compression;

/// <summary>
/// Creates compressors by name from the built-in formats and registered plugins
/// </summary>
public sealed class CompressorFactory : ICompressorFactory
{
    /// <summary>
    /// The most input bytes a compressor receives for one block
    /// </summary>
    public const int MaxInputLength = 16 * 1024 * 1024;

    private readonly Dictionary<string, (Func<ICompressionPlugin> Create, string Source)> _compressors = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CompressionInfo> _infos = [];

    public CompressorFactory()
    {
        AddBuiltIn(() => new GbaLz77Compression());
        AddBuiltIn(() => new GbaRleCompression());
    }

    private void AddBuiltIn(Func<ICompressionPlugin> create)
    {
        var info = create().Info;
        _compressors.Add(info.Name, (create, "a built-in format"));
        _infos.Add(info);
    }

    public MagitekResult<string> AddCompressionPlugin([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type pluginType)
    {
        if (pluginType.IsAbstract)
            return new MagitekResult<string>.Failed($"Compression plugin type '{pluginType}' is abstract");

        if (!pluginType.IsAssignableTo(typeof(ICompressionPlugin)))
            return new MagitekResult<string>.Failed($"Compression plugin type '{pluginType}' does not implement {nameof(ICompressionPlugin)}");

        if (pluginType.GetConstructor(Type.EmptyTypes) is null)
            return new MagitekResult<string>.Failed($"Compression plugin type '{pluginType}' has no public parameterless constructor");

        CompressionInfo? info;
        try
        {
            info = ((ICompressionPlugin)Activator.CreateInstance(pluginType)!).Info;
        }
        catch (Exception ex)
        {
            var reason = ex is TargetInvocationException { InnerException: { } inner } ? inner.Message : ex.Message;
            return new MagitekResult<string>.Failed($"Compression plugin type '{pluginType}' could not be created: {reason}");
        }

        if (info is null || string.IsNullOrWhiteSpace(info.Name))
            return new MagitekResult<string>.Failed($"Compression plugin type '{pluginType}' was not registered because its {nameof(CompressionInfo.Name)} is blank");

        if (_compressors.TryGetValue(info.Name, out var existing))
            return new MagitekResult<string>.Failed($"Compression plugin type '{pluginType}' was not registered because its name '{info.Name}' is already registered by {existing.Source}");

        _compressors.Add(info.Name, (() => (ICompressionPlugin)Activator.CreateInstance(pluginType)!, $"'{pluginType}'"));
        _infos.Add(info);
        return new MagitekResult<string>.Success(info.Name);
    }

    public ICompressionPlugin? CreateCompressor(string name) =>
        _compressors.TryGetValue(name, out var entry) ? entry.Create() : null;

    public IReadOnlyList<CompressionInfo> GetRegisteredCompressors() => _infos.ToList();
}
