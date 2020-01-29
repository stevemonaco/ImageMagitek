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
    }
}
