using System.Collections.Generic;

namespace ImageMagitek.Codec
{
    public interface IGraphicsFormat
    {
        int ColorDepth { get; set; }
        PixelColorType ColorType { get; set; }
        
        int DefaultHeight { get; set; }
        int DefaultWidth { get; set; }

        int ElementStride { get; set; }
        bool FixedSize { get; set; }

        int Height { get; set; }
        int Width { get; set; }

        string Name { get; set; }
        int RowStride { get; set; }
        int StorageSize { get; }

        IGraphicsFormat Clone();
        void Resize(int width, int height);
    }
}