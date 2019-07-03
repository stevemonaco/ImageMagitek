using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Codec
{
    public interface ICodecFactory
    {
        IGraphicsCodec GetCodec(string codecName);
    }
}
