using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

/// <summary>
/// Manages the creation of codecs from default (built-in), IGraphicsCodec (XML-based), and/or plugin codec sources
/// </summary>
public sealed class CodecFactory : ICodecFactory
{
    public Palette DefaultPalette { get; set; }

    private readonly Dictionary<string, IGraphicsFormat> _formats;
    private readonly Dictionary<string, Type> _codecs;

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
    /// Registers a C# codec type under the name its instance reports
    /// </summary>
    /// <returns>A failure when the type cannot be instantiated or its name is already registered</returns>
    public MagitekResult AddCodec(Type codecType)
    {
        if (!typeof(IGraphicsCodec).IsAssignableFrom(codecType) || codecType.IsAbstract)
            return new MagitekResult.Failed($"Codec type '{codecType}' is not of type {typeof(IGraphicsCodec)} or is not instantiable");

        string? name;
        try
        {
            name = CreateInstance(codecType, null).Name;
        }
        catch (Exception ex)
        {
            var reason = ex is TargetInvocationException { InnerException: { } inner } ? inner.Message : ex.Message;
            return new MagitekResult.Failed($"Codec type '{codecType}' could not be created: {reason}");
        }

        if (string.IsNullOrWhiteSpace(name))
            return new MagitekResult.Failed($"Codec type '{codecType}' was not registered because it has no name");

        if (FindRegistration(name) is { } existing)
            return new MagitekResult.Failed($"Codec type '{codecType}' was not registered because its name '{name}' is already registered by {existing}");

        _codecs.Add(name, codecType);
        return MagitekResult.SuccessResult;
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
        if (!_codecs.ContainsKey(codecName) && !_formats.ContainsKey(codecName) && _legacyCodecNames.TryGetValue(codecName, out var currentName))
            codecName = currentName;

        if (_codecs.TryGetValue(codecName, out var codecType))
            return CreateInstance(codecType, elementSize);
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

    private IGraphicsCodec CreateInstance(Type codecType, Size? elementSize)
    {
        var isIndexed = codecType.IsAssignableTo(typeof(IIndexedCodec));
        Type[] leadingTypes = isIndexed ? [typeof(Palette)] : [];
        object[] leadingArgs = isIndexed ? [DefaultPalette] : [];

        if (elementSize is Size size && codecType.GetConstructor([.. leadingTypes, typeof(int), typeof(int)]) is { } sizedConstructor)
            return (IGraphicsCodec)sizedConstructor.Invoke([.. leadingArgs, size.Width, size.Height]);

        if (codecType.GetConstructor(leadingTypes) is { } constructor)
            return (IGraphicsCodec)constructor.Invoke(leadingArgs);

        throw new ArgumentException($"Codec type '{codecType}' has no supported constructor");
    }

    public IEnumerable<string> GetRegisteredCodecNames()
    {
        return _formats.Keys
            .Concat(_codecs.Keys)
            .OrderBy(x => x);
    }
}
