using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Codec
{
    public class CodecFactory : ICodecFactory
    {
        private Dictionary<string, GraphicsFormat> Formats;

        public CodecFactory(Dictionary<string, GraphicsFormat> formats)
        {
            if (formats is null)
                Formats = new Dictionary<string, GraphicsFormat>();
            else
                Formats = formats;
        }

        public IGraphicsCodec GetCodec(string codecName, int width = 8, int height = 8)
        {
            switch (codecName)
            {
                case "SNES 3bpp":
                    return new SNES3bppCodec(width, height);
                default:
                    if (Formats.ContainsKey(codecName))
                    {
                        var format = Formats[codecName].Clone();
                        format.Width = 8;
                        format.Height = 8;

                        return new GenericGraphicsCodec(format);
                    }
                    else
                        throw new KeyNotFoundException($"{nameof(GetCodec)} could not locate a codec for '{nameof(codecName)}'");
            }
        }
    }
}
