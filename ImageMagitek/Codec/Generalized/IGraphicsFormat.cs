namespace ImageMagitek.Codec
{
    public interface IGraphicsFormat
    {
        string Name { get; }

        PixelColorType ColorType { get; }
        int ColorDepth { get; }

        ImageLayout Layout { get; }

        int DefaultHeight { get; }
        int DefaultWidth { get; }

        int Height { get; }
        int Width { get; }

        bool FixedSize { get; }
        int StorageSize { get; }

        IGraphicsFormat Clone();
    }
}