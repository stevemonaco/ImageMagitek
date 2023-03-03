using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ImageMagitek.Codec;

public sealed class CodecFactory : ICodecFactory
{
    private readonly Dictionary<string, IGraphicsFormat> _formats;
    private readonly Dictionary<string, Type> _codecs;

    public CodecFactory(Dictionary<string, IGraphicsFormat> formats)
    {
        _formats = formats ?? new Dictionary<string, IGraphicsFormat>();

        _codecs = new Dictionary<string, Type>
        {
            // Initialize with built-in codecs
            { "SNES 3bpp", typeof(Snes3bppCodec) }, { "PSX 4bpp", typeof(Psx4bppCodec) }, { "PSX 8bpp", typeof(Psx8bppCodec) },
            { "Rgb24 Tiled", typeof(Rgb24TiledCodec) }, { "Rgba32 Tiled", typeof(Rgba32TiledCodec) }, { "Bmp24", typeof(Bmp24Codec)},
            { "N64 Rgba16", typeof(N64Rgba16Codec) }, { "N64 Rgba32", typeof(N64Rgba32Codec) },
            { "PSX 16bpp", typeof(Psx16bppCodec) }, { "PSX 24bpp", typeof(Psx24bppCodec) }
        };
    }

    public void AddOrUpdateCodec(Type codecType)
    {
        if (typeof(IGraphicsCodec).IsAssignableFrom(codecType) && !codecType.IsAbstract)
        {
            if (Activator.CreateInstance(codecType) is IGraphicsCodec codec)
                _codecs[codec.Name] = codecType;
        }
        else
            throw new ArgumentException($"{nameof(AddOrUpdateCodec)} parameter '{nameof(codecType)}' is not of type {typeof(IGraphicsCodec)} or is not instantiable");
    }

    public IGraphicsCodec? GetCodec(string codecName, Size? elementSize = default)
    {
        if (_codecs.ContainsKey(codecName)) // Prefer built-in codecs
        {
            var codecType = _codecs[codecName];
            if (elementSize.HasValue)
                return Activator.CreateInstance(codecType, elementSize.Value.Width, elementSize.Value.Height) as IGraphicsCodec;
            else
                return Activator.CreateInstance(codecType) as IGraphicsCodec;
        }
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
                    return new IndexedFlowGraphicsCodec(flowFormat);
                else if (format.ColorType == PixelColorType.Direct)
                    throw new NotSupportedException();
            }
            else if (format is PatternGraphicsFormat patternFormat)
            {
                if (format.ColorType == PixelColorType.Indexed)
                    return new IndexedPatternGraphicsCodec(patternFormat);
                else if (format.ColorType == PixelColorType.Direct)
                    throw new NotSupportedException();
            }

            throw new NotSupportedException($"Graphics format of type '{format}' is not supported");
        }
        else
        {
            throw new KeyNotFoundException($"{nameof(GetCodec)} could not locate a codec for '{codecName}'");
        }
    }

    public IGraphicsCodec? CloneCodec(IGraphicsCodec codec)
    {
        return GetCodec(codec.Name, new Size(codec.Width, codec.Height));
    }

    public IEnumerable<string> GetSupportedCodecNames()
    {
        return _formats.Keys
            .Concat(_codecs.Keys)
            .OrderBy(x => x);
    }
}
