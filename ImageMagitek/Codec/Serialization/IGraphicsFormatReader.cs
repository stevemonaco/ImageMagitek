namespace ImageMagitek.Codec
{
    public interface IGraphicsFormatReader
    {
        MagitekResults<FlowGraphicsFormat> LoadFromFile(string fileName);
    }
}
