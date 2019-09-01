using System.Collections.Generic;

namespace ImageMagitek.Codec
{
    public interface ICodecFactory
    {
        IGraphicsCodec GetCodec(string codecName, int width = 8, int height = 8);
        IEnumerable<string> GetSupportedCodecNames();
    }
}
