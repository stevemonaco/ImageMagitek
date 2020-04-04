namespace ImageMagitek.Codec
{
    /// <summary>
    /// Specifies how the graphical viewer should treat the graphic
    /// Tiled is capable of rendering a grid of multiple images
    /// Single will render a single image
    /// </summary>
    public enum ImageLayout { Tiled = 0, Single }

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

        int DefaultWidth { get; }
        int DefaultHeight { get; }
        bool CanResize { get; }
        int WidthResizeIncrement { get; }
        int HeightResizeIncrement { get; }
        int GetPreferredWidth(int width);
        int GetPreferredHeight(int height);
    }
}
