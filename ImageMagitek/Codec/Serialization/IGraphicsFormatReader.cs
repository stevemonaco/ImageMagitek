namespace ImageMagitek.Codec
{
    public interface IGraphicsFormatReader
    {
        MagitekResults<GraphicsFormat> LoadFromFile(string fileName);
    }
}
