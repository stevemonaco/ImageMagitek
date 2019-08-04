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
    }
}
