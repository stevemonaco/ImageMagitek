using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ImageMagitek.Codec
{
    public sealed class CodecFactory : ICodecFactory
    {
        private readonly Dictionary<string, IGraphicsFormat> _formats;
        private readonly Dictionary<string, Type> _codecs;

        public CodecFactory(Dictionary<string, IGraphicsFormat> formats)
        {
            _formats = formats ?? new Dictionary<string, IGraphicsFormat>();
            _codecs = new Dictionary<string, Type>
            {
                { "SNES 3bpp", typeof(Snes3bppCodec) }, { "PSX 4bpp", typeof(Psx4bppCodec) }, { "PSX 8bpp", typeof(Psx8bppCodec) },
                { "Rgb24 Tiled", typeof(Rgb24TiledCodec) }, { "Rgba32 Tiled", typeof(Rgba32TiledCodec) }, { "Rgba16 Tiled", typeof(Rgba16TiledCodec) },
                { "PSX 16bpp", typeof(Psx16bppCodec) }, { "PSX 24bpp", typeof(Psx24bppCodec) }
            };
        }

        public void AddOrUpdateCodec(Type codecType)
        {
            if (typeof(IGraphicsCodec).IsAssignableFrom(codecType) && !codecType.IsAbstract)
            {
                var codec = (IGraphicsCodec)Activator.CreateInstance(codecType);
                _codecs[codec.Name] = codecType;
            }
            else
                throw new ArgumentException($"{nameof(AddOrUpdateCodec)} parameter '{nameof(codecType)}' is not of type {typeof(IGraphicsCodec)} or is not instantiable");
        }

        public IGraphicsCodec GetCodec(string codecName, Size? elementSize)
        {
            if (_codecs.ContainsKey(codecName))
            {
                var codecType = _codecs[codecName];
                if (elementSize.HasValue)
                    return (IGraphicsCodec) Activator.CreateInstance(codecType, elementSize.Value.Width, elementSize.Value.Height);
                else
                    return (IGraphicsCodec) Activator.CreateInstance(codecType);
            }
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
                        return new IndexedFlowGraphicsCodec(flowFormat);
                    else if (format.ColorType == PixelColorType.Direct)
                        throw new NotImplementedException();
                }
                else if (format is PatternGraphicsFormat patternFormat)
                {
                    if (format.ColorType == PixelColorType.Indexed)
                        return new IndexedPatternGraphicsCodec(patternFormat);
                    else if (format.ColorType == PixelColorType.Direct)
                        throw new NotImplementedException();
                }

                throw new NotSupportedException($"Graphics format of type '{format}' is not supported");
            }
            else
            {
                throw new KeyNotFoundException($"{nameof(GetCodec)} could not locate a codec for '{codecName}'");
            }
        }

        public IGraphicsCodec CloneCodec(IGraphicsCodec codec)
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
}
