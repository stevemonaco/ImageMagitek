using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

public sealed class N64Rgba32Codec : DirectCodec
{
    public override string Name => "N64 Rgba32";
    public override int Width { get; } = 32;
    public override int Height { get; } = 32;
    public override ImageLayout Layout => ImageLayout.Tiled;
    public override int ColorDepth => 32;
    public override int StorageSize => Width * Height * 32;
    public override int RowStride { get; } = 0;
    public override int ElementStride { get; } = 0;
    public override bool CanEncode => true;

    public override bool CanResize => true;
    public override int WidthResizeIncrement => 1;
    public override int HeightResizeIncrement => 1;
    public override int DefaultWidth => 32;
    public override int DefaultHeight => 32;

    public N64Rgba32Codec()
    {
    }

    public N64Rgba32Codec(int width, int height) : base(width, height)
    {
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
                var g = encodedBuffer[src];
                var r = encodedBuffer[src + 1];
                var a = encodedBuffer[src + 2];
                var b = encodedBuffer[src + 3];

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
                _foreignBuffer[dest] = imageColor.G;
                _foreignBuffer[dest + 1] = imageColor.R;
                _foreignBuffer[dest + 2] = imageColor.A;
                _foreignBuffer[dest + 3] = imageColor.B;
            }
        }

        return _foreignBuffer;
    }
}
