namespace ImageMagitek.Codec
{
    public interface IGraphicsFormatReader
    {
        GraphicsFormat LoadFromFile(string fileName);
    }
}
