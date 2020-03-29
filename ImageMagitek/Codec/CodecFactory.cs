using System;
using System.Collections.Generic;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec
{
    public class CodecFactory : ICodecFactory
    {
        private readonly Dictionary<string, GraphicsFormat> _formats;
        public Palette DefaultPalette { get; set; }

        public CodecFactory(Dictionary<string, GraphicsFormat> formats, Palette defaultPalette)
        {
            _formats = formats ?? new Dictionary<string, GraphicsFormat>();
            DefaultPalette = defaultPalette;
        }

        public IGraphicsCodec GetCodec(string codecName, int width = 8, int height = 8, int rowStride = 0)
        {
            switch (codecName)
            {
                case "SNES 3bpp":
                    return new Snes3bppCodec(width, height);
                case "PSX 4bpp":
                    return new Psx4bppCodec(width, height);
                case "PSX 8bpp":
                    return new Psx8bppCodec(width, height);
                //case "PSX 16bpp":
                //    return new Psx16bppCodec(width, height);
                //case "PSX 24bpp":
                //    return new Psx24bppCodec(width, height);
                default:
                    if (_formats.ContainsKey(codecName))
                    {
                        var format = _formats[codecName].Clone();
                        format.Name = codecName;
                        format.Width = width;
                        format.Height = height;

                        if (format.ColorType == PixelColorType.Indexed)
                            return new IndexedGraphicsCodec(format, DefaultPalette);
                        else if (format.ColorType == PixelColorType.Direct)
                            throw new NotSupportedException();

                        throw new NotSupportedException();
                    }
                    else
                        throw new KeyNotFoundException($"{nameof(GetCodec)} could not locate a codec for '{nameof(codecName)}'");
            }
        }

        public IGraphicsCodec CloneCodec(IGraphicsCodec codec)
        {
            return GetCodec(codec.Name, codec.Width, codec.Height, codec.RowStride);
        }

        public IEnumerable<string> GetSupportedCodecNames()
        {
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
