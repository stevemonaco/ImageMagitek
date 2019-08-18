using System.Collections.Generic;

namespace ImageMagitek.Codec
{
    public class CodecFactory : ICodecFactory
    {
        private Dictionary<string, GraphicsFormat> Formats;

        public CodecFactory(Dictionary<string, GraphicsFormat> formats)
        {
            Formats = formats ?? new Dictionary<string, GraphicsFormat>();
        }

        public IGraphicsCodec GetCodec(string codecName, int width = 8, int height = 8)
        {
            switch (codecName)
            {
                case "SNES 3bpp":
                    return new Snes3bppCodec(width, height);
                case "PSX 4bpp":
                    return new Psx4bppCodec(width, height);
                case "PSX 8bpp":
                    return new Psx8bppCodec(width, height);
                case "PSX 16bpp":
                    return new Psx16bppCodec(width, height);
                case "PSX 24bpp":
                    return new Psx24bppCodec(width, height);
                default:
                    if (Formats.ContainsKey(codecName))
                    {
                        var format = Formats[codecName].Clone();
                        format.Width = width;
                        format.Height = height;

                        return new GeneralGraphicsCodec(format);
                    }
                    else
                        throw new KeyNotFoundException($"{nameof(GetCodec)} could not locate a codec for '{nameof(codecName)}'");
            }
        }

        public IEnumerable<string> GetSupportedCodecNames()
        {
            yield return "SNES 3bpp";
            yield return "PSX 4bpp";
            yield return "PSX 8bpp";
            yield return "PSX 16bpp";
            yield return "PSX 24bpp";

            foreach (var format in Formats.Values)
                yield return format.Name;
        }
    }
}
