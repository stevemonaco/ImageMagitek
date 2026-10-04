using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

public sealed class Rgba32TiledCodec : DirectCodec
{
    public override string Name => "Rgba32 Tiled";
    public override int Width { get; } = 8;
    public override int Height { get; } = 8;
    public override ImageLayout Layout => ImageLayout.Tiled;
    public override int ColorDepth => 32;
    public override int StorageSize => Width * Height * 32;
    public override int RowStride { get; } = 0;
    public override int ElementStride { get; } = 0;
    public override bool CanEncode => true;

    public override bool CanResize => true;
    public override int WidthResizeIncrement => 1;
    public override int HeightResizeIncrement => 1;
    public override int DefaultWidth => 8;
    public override int DefaultHeight => 8;

    public Rgba32TiledCodec()
    {
        Width = DefaultWidth;
        Height = DefaultHeight;

        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new ColorRgba32[Height, Width];
    }

    public Rgba32TiledCodec(int width, int height)
    {
        Width = width;
        Height = height;

        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new ColorRgba32[Height, Width];
    }

    public override ColorRgba32[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize)
            throw new ArgumentException(nameof(encodedBuffer));

        int src = 0;
        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++, src += 4)
            {
                var r = encodedBuffer[src];
                var g = encodedBuffer[src + 1];
                var b = encodedBuffer[src + 2];
                var a = encodedBuffer[src + 3];

                _nativeBuffer[y, x] = new ColorRgba32(r, g, b, a);
            }
        }

        return NativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, ColorRgba32[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        int dest = 0;
        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++, dest += 4)
            {
                var imageColor = imageBuffer[y, x];
                _foreignBuffer[dest] = imageColor.R;
                _foreignBuffer[dest + 1] = imageColor.G;
                _foreignBuffer[dest + 2] = imageColor.B;
                _foreignBuffer[dest + 3] = imageColor.A;
            }
        }

        return _foreignBuffer;
    }
}
