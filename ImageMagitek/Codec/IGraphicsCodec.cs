using ImageMagitek.Colors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageMagitek.Codec
{
    /// <summary>
    /// Specifies how the graphical viewer will treat the graphic
    /// Tiled graphics will render a grid of multiple images
    /// Linear graphics will render a single image
    /// </summary>
    public enum ImageLayout { Tiled = 0, Linear }

    /// <summary>
    /// Specifies how the pixels' colors are determined for the graphic
    /// Indexed graphics have their full color determined by a palette
    /// Direct graphics have their full color determined by the pixel image data alone
    /// </summary>
    public enum PixelColorType { Indexed = 0, Direct }

    public interface IGraphicsCodec
    {
        string Name { get; }
        int Width { get; }
        int Height { get; }
        ImageLayout Layout { get; }
        PixelColorType ColorType { get; }
        int ColorDepth { get; }
        int StorageSize { get; }
        int RowStride { get; }
        int ElementStride { get; }

        void Decode(Image<Rgba32> image, ArrangerElement el);
        void Encode(Image<Rgba32> image, ArrangerElement el);
    }
}
