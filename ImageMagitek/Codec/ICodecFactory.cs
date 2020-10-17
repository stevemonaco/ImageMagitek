using System.Collections.Generic;
using System.Drawing;

namespace ImageMagitek.Codec
{
    public interface ICodecFactory
    {
        IGraphicsCodec GetCodec(string codecName, Size? elementSize);
        IGraphicsCodec CloneCodec(IGraphicsCodec codec);
        IEnumerable<string> GetSupportedCodecNames();
    }
}
