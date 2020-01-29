namespace ImageMagitek.Codec
{
    public interface IDirectGraphicsCodec : IGraphicsCodec
    {
        void Decode(DirectImage image, ArrangerElement el);
        void Encode(DirectImage image, ArrangerElement el);
    }
}