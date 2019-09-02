using System.Collections.Generic;

namespace ImageMagitek.Codec
{
    public interface ICodecFactory
    {
        IGraphicsCodec GetCodec(string codecName, int width = 8, int height = 8, int rowStride = 0);
        IGraphicsCodec CloneCodec(IGraphicsCodec codec);
        IEnumerable<string> GetSupportedCodecNames();
    }
}
