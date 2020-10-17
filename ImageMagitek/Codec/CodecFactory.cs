using System;
using System.Collections.Generic;
using System.Drawing;

namespace ImageMagitek.Codec
{
    public class CodecFactory : ICodecFactory
    {
        private readonly Dictionary<string, IGraphicsFormat> _formats;

        public CodecFactory(Dictionary<string, IGraphicsFormat> formats)
        {
            _formats = formats ?? new Dictionary<string, IGraphicsFormat>();
        }

        public IGraphicsCodec GetCodec(string codecName, Size? elementSize)
        {
            switch (codecName)
            {
                //case "NES 1bpp":
                //    return new Nes1bppCodec(width, height);
                case "SNES 3bpp":
                    return elementSize.HasValue ? new Snes3bppCodec(elementSize.Value.Width, elementSize.Value.Height) : new Snes3bppCodec();
                case "PSX 4bpp":
                    return elementSize.HasValue ? new Psx4bppCodec(elementSize.Value.Width, elementSize.Value.Height) : new Psx4bppCodec();
                case "PSX 8bpp":
                    return elementSize.HasValue ? new Psx8bppCodec(elementSize.Value.Width, elementSize.Value.Height) : new Psx8bppCodec();
                //case "PSX 16bpp":
                //    return new Psx16bppCodec(width, height);
                //case "PSX 24bpp":
                //    return new Psx24bppCodec(width, height);
                default:
                    if (_formats.ContainsKey(codecName))
                    {
                        var format = _formats[codecName].Clone() as FlowGraphicsFormat;

                        if (elementSize.HasValue)
                        {
                            format.Width = elementSize.Value.Width;
                            format.Height = elementSize.Value.Height;
                        }

                        if (format.ColorType == PixelColorType.Indexed)
                            return new IndexedGraphicsCodec(format);
                        else if (format.ColorType == PixelColorType.Direct)
                            throw new NotSupportedException();

                        throw new NotSupportedException();
                    }
                    else
                        throw new KeyNotFoundException($"{nameof(GetCodec)} could not locate a codec for '{codecName}'");
            }
        }

        public IGraphicsCodec CloneCodec(IGraphicsCodec codec)
        {
            return GetCodec(codec.Name, new Size(codec.Width, codec.Height));
        }

        public IEnumerable<string> GetSupportedCodecNames()
        {
            //yield return "NES 1bpp";
            yield return "SNES 3bpp";
            yield return "PSX 4bpp";
            yield return "PSX 8bpp";
            //yield return "PSX 16bpp";
            //yield return "PSX 24bpp";

            foreach (var format in _formats.Values)
                yield return format.Name;
        }
    }
}
