namespace ImageMagitek.Codec
{
    public interface ICodecFactory
    {
        IGraphicsCodec GetCodec(string codecName, int width, int height);
    }
}
