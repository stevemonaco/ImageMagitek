using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using ImageMagitek.Colors;
using ImageMagitek.Plugins;

namespace ImageMagitek.Codec;

/// <summary>
/// Manages the creation of codecs from default (built-in), IGraphicsCodec (XML-based), and/or plugin codec sources
/// </summary>
public sealed class CodecFactory : ICodecFactory
{
    public Palette DefaultPalette { get; set; }

    private readonly Dictionary<string, IGraphicsFormat> _formats;
    private readonly Dictionary<string, Type> _codecs;
    private readonly Dictionary<string, Type> _codecPlugins = [];

    // Names of retired built-in codecs, kept so existing projects resolve to their byte-identical XML codecs
    private static readonly Dictionary<string, string> _legacyCodecNames = new()
    {
        ["SNES 3bpp"] = "SNES 3bpp Flow",
        ["PSX 4bpp"] = "PSX 4bpp Flow",
        ["PSX 8bpp"] = "PSX 8bpp Flow",
    };

    /// <summary>
    /// Creates a CodecFactory and registers default codecs and provided formats
    /// </summary>
    /// <param name="defaultPalette">Palette to automatically assign to indexed codecs</param>
    /// <param name="formats">Formats to automatically register. May be empty.</param>
    public CodecFactory(Palette defaultPalette, Dictionary<string, IGraphicsFormat> formats)
    {
        DefaultPalette = defaultPalette;
        _formats = formats;

        _codecs = new Dictionary<string, Type>
        {
            // Initialize with built-in codecs
            { "Rgb24 Tiled", typeof(Rgb24TiledCodec) }, { "Rgba32 Tiled", typeof(Rgba32TiledCodec) }, { "Bmp24", typeof(Bmp24Codec)},
            { "N64 Rgba16", typeof(N64Rgba16Codec) }, { "N64 Rgba32", typeof(N64Rgba32Codec) },
            { "PSX 16bpp", typeof(Psx16BppCodec) }, { "PSX 24bpp", typeof(Psx24BppCodec) }
        };
    }

    /// <summary>
    /// Registers a plugin codec type under its <see cref="CodecInfo.Name"/> after validating the type and its <see cref="CodecInfo"/>
    /// </summary>
    /// <returns>The registered name, or a failure naming the type and the reason it was refused</returns>
    public MagitekResult<string> AddCodecPlugin(Type pluginType)
    {
        var isIndexed = pluginType.IsAssignableTo(typeof(IIndexedCodecPlugin));
        var isDirect = pluginType.IsAssignableTo(typeof(IDirectCodecPlugin));

        if (pluginType.IsAbstract)
            return new MagitekResult<string>.Failed($"Plugin codec type '{pluginType}' is abstract");

        if (isIndexed == isDirect)
            return new MagitekResult<string>.Failed($"Plugin codec type '{pluginType}' must implement exactly one of {nameof(IIndexedCodecPlugin)} and {nameof(IDirectCodecPlugin)}");

        if (pluginType.GetConstructor(Type.EmptyTypes) is null)
            return new MagitekResult<string>.Failed($"Plugin codec type '{pluginType}' has no public parameterless constructor");

        ICodecPlugin plugin;
        CodecInfo? info;
        try
        {
            plugin = (ICodecPlugin)Activator.CreateInstance(pluginType)!;
            info = plugin.Info;
        }
        catch (Exception ex)
        {
            var reason = ex is TargetInvocationException { InnerException: { } inner } ? inner.Message : ex.Message;
            return new MagitekResult<string>.Failed($"Plugin codec type '{pluginType}' could not be created: {reason}");
        }

        if (ValidateInfo(plugin, info, isIndexed) is { } invalid)
            return new MagitekResult<string>.Failed($"Plugin codec type '{pluginType}' was not registered because {invalid}");

        if (FindRegistration(info!.Name) is { } existing)
            return new MagitekResult<string>.Failed($"Plugin codec type '{pluginType}' was not registered because its name '{info.Name}' is already registered by {existing}");

        _codecPlugins.Add(info.Name, pluginType);
        return new MagitekResult<string>.Success(info.Name);
    }

    private static string? ValidateInfo(ICodecPlugin plugin, CodecInfo? info, bool isIndexed)
    {
        if (info is null)
            return $"its {nameof(ICodecPlugin.Info)} is null";

        if (string.IsNullOrWhiteSpace(info.Name))
            return $"its {nameof(CodecInfo.Name)} is blank";

        var maxDepth = isIndexed ? 8 : 32;
        if (info.ColorDepth < 1 || info.ColorDepth > maxDepth)
            return $"'{info.Name}' has {nameof(CodecInfo.ColorDepth)} {info.ColorDepth}, which must be 1 to {maxDepth}";

        if (info.DefaultWidth < 1)
            return $"'{info.Name}' has {nameof(CodecInfo.DefaultWidth)} {info.DefaultWidth}, which must be at least 1";

        if (info.DefaultHeight < 1)
            return $"'{info.Name}' has {nameof(CodecInfo.DefaultHeight)} {info.DefaultHeight}, which must be at least 1";

        if (info.WidthResizeIncrement < 0)
            return $"'{info.Name}' has a negative {nameof(CodecInfo.WidthResizeIncrement)}";

        if (info.HeightResizeIncrement < 0)
            return $"'{info.Name}' has a negative {nameof(CodecInfo.HeightResizeIncrement)}";

        int storageBits;
        try
        {
            storageBits = plugin.GetStorageBits(info.DefaultWidth, info.DefaultHeight);
        }
        catch (Exception ex)
        {
            return $"'{info.Name}' threw from {nameof(ICodecPlugin.GetStorageBits)}: {ex.Message}";
        }

        if (storageBits < 1)
            return $"'{info.Name}' returns {storageBits} from {nameof(ICodecPlugin.GetStorageBits)} at its default size, which must be at least 1";

        return null;
    }

    /// <summary>
    /// Registers a generalized graphics format under its name
    /// </summary>
    /// <returns>A failure when the name is already registered</returns>
    public MagitekResult AddFormat(IGraphicsFormat format)
    {
        if (FindRegistration(format.Name) is { } existing)
            return new MagitekResult.Failed($"Codec '{format.Name}' was not registered because the name is already registered by {existing}");

        _formats.Add(format.Name, format);
        return MagitekResult.SuccessResult;
    }

    private string? FindRegistration(string name)
    {
        if (_codecs.TryGetValue(name, out var codecType))
            return $"codec type '{codecType}'";

        if (_codecPlugins.TryGetValue(name, out var pluginType))
            return $"plugin codec type '{pluginType}'";

        if (_formats.ContainsKey(name))
            return $"XML codec '{name}'";

        return null;
    }

    /// <summary>
    /// Creates a new instance of a codec registered with the given name and optional size
    /// </summary>
    /// <param name="codecName">Name of the codec to create</param>
    /// <param name="elementSize">Size in pixels of the element the codec operates on</param>
    /// <returns>The codec if successful, null if not</returns>
    /// <exception cref="NotSupportedException">A format type was registered that cannot be created</exception>
    /// <exception cref="KeyNotFoundException">The codec name was not registered</exception>
    public IGraphicsCodec? CreateCodec(string codecName, Size? elementSize = default)
    {
        if (FindRegistration(codecName) is null && _legacyCodecNames.TryGetValue(codecName, out var currentName))
            codecName = currentName;

        if (_codecs.TryGetValue(codecName, out var codecType))
            return CreateInstance(codecType, elementSize);
        else if (_codecPlugins.TryGetValue(codecName, out var pluginType))
            return CreatePluginInstance(pluginType, elementSize);
        else if (_formats.ContainsKey(codecName))
        {
            var format = _formats[codecName].Clone();

            if (format is FlowGraphicsFormat flowFormat)
            {
                if (elementSize.HasValue)
                {
                    flowFormat.Width = elementSize.Value.Width;
                    flowFormat.Height = elementSize.Value.Height;
                }

                if (format.ColorType == PixelColorType.Indexed)
                    return new IndexedFlowGraphicsCodec(flowFormat, DefaultPalette);
                else if (format.ColorType == PixelColorType.Direct)
                    throw new NotSupportedException();
            }
            else if (format is PatternGraphicsFormat patternFormat)
            {
                if (format.ColorType == PixelColorType.Indexed)
                    return new IndexedPatternGraphicsCodec(patternFormat, DefaultPalette);
                else if (format.ColorType == PixelColorType.Direct)
                    throw new NotSupportedException();
            }

            throw new NotSupportedException($"Graphics format of type '{format}' is not supported");
        }
        else
        {
            throw new KeyNotFoundException($"{nameof(CreateCodec)} could not locate a codec for '{codecName}'");
        }
    }

    /// <summary>
    /// Creates a new instance of the given codec with the same dimensions
    /// </summary>
    /// <param name="codec">The codec to be cloned</param>
    /// <returns>A valid codec</returns>
    public IGraphicsCodec CloneCodec(IGraphicsCodec codec)
    {
        var clonedCodec = CreateCodec(codec.Name, new Size(codec.Width, codec.Height));

        if (clonedCodec is null)
            throw new ArgumentException($"Could not clone Codec '{codec.Name}'");

        return clonedCodec;
    }

    private static IGraphicsCodec CreateInstance(Type codecType, Size? elementSize)
    {
        if (elementSize is Size size && codecType.GetConstructor([typeof(int), typeof(int)]) is { } sizedConstructor)
            return (IGraphicsCodec)sizedConstructor.Invoke([size.Width, size.Height]);

        if (codecType.GetConstructor(Type.EmptyTypes) is { } constructor)
            return (IGraphicsCodec)constructor.Invoke([]);

        throw new ArgumentException($"Codec type '{codecType}' has no supported constructor");
    }

    private IGraphicsCodec CreatePluginInstance(Type pluginType, Size? elementSize)
    {
        return Activator.CreateInstance(pluginType) switch
        {
            IIndexedCodecPlugin indexed => new IndexedCodecPluginAdapter(indexed, DefaultPalette, elementSize?.Width, elementSize?.Height),
            IDirectCodecPlugin direct => new DirectCodecPluginAdapter(direct, elementSize?.Width, elementSize?.Height),
            _ => throw new NotSupportedException($"Plugin codec type '{pluginType}' is not a supported plugin kind")
        };
    }

    public IEnumerable<string> GetRegisteredCodecNames()
    {
        return _formats.Keys
            .Concat(_codecs.Keys)
            .Concat(_codecPlugins.Keys)
            .OrderBy(x => x);
    }
}
