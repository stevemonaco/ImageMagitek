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

        public IGraphicsCodec GetCodec(string codecName)
        {
            switch(codecName)
            {
                case "SNES3bpp":
                    return new SNES3bppCodec();
                default:
                    if (Formats.ContainsKey(codecName))
                        return new GenericGraphicsCodec(Formats[codecName].Clone());
                    else
                        throw new KeyNotFoundException($"{nameof(GetCodec)} could not locate a codec for '{nameof(codecName)}'");
            }
        }
    }
}
