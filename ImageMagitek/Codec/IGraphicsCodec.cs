using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek
{
    public interface IGraphicsCodec
    {
        string Name { get; }
        int StorageSize { get; }
        void Decode(Image<Rgba32> image, ArrangerElement el);
        void Encode(Image<Rgba32> image, ArrangerElement el);
    }
}
