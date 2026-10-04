using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

    public void AddOrUpdateCodec(Type codecType)
    {
        if (!typeof(IGraphicsCodec).IsAssignableFrom(codecType) || codecType.IsAbstract)
            throw new ArgumentException($"{nameof(AddOrUpdateCodec)} parameter '{nameof(codecType)}' is not of type {typeof(IGraphicsCodec)} or is not instantiable");

        var codec = CreateInstance(codecType, null);
        _codecs[codec.Name] = codecType;
    }

    public void AddOrUpdateFormat(IGraphicsFormat format)
    {
        _formats[format.Name] = format;
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

        if (_codecs.TryGetValue(codecName, out var codecType)) // Prefer built-in codecs
            return CreateInstance(codecType, elementSize);
        else if (_formats.ContainsKey(codecName)) // Fallback to generalized codecs
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
