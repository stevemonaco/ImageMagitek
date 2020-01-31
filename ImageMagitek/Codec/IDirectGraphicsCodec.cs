using ImageMagitek.Colors;

namespace ImageMagitek.Codec
{
    public interface IDirectGraphicsCodec : IGraphicsCodec
    {
        void Decode(ArrangerElement el, ColorRgba32[,] imageBuffer);
        void Encode(ArrangerElement el, ColorRgba32[,] imageBuffer);
    }
}