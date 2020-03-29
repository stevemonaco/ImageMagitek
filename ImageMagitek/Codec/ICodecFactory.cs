using System.Collections.Generic;

namespace ImageMagitek.Codec
{
    public interface ICodecFactory
    {
        IGraphicsCodec GetCodec(string codecName);
        IGraphicsCodec GetCodec(string codecName, int width, int height, int rowStride = 0);
        IGraphicsCodec CloneCodec(IGraphicsCodec codec);
        IEnumerable<string> GetSupportedCodecNames();
    }
}
