namespace ImageMagitek.Codec
{
    public interface IIndexedGraphicsCodec : IGraphicsCodec
    {
        void Decode(ArrangerElement el, byte[,] imageBuffer);
        void Encode(ArrangerElement el, byte[,] imageBuffer);
    }
}