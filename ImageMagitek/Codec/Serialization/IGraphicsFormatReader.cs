namespace ImageMagitek.Codec
{
    public interface IGraphicsFormatReader
    {
        MagitekResults<IGraphicsFormat> LoadFromFile(string fileName);
    }
}
