using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

public sealed class Bmp24Codec : DirectCodec
{
    public override string Name => "Bmp24";
    public override ImageLayout Layout => ImageLayout.Single;
    public override int ColorDepth => 24;
    public override int StorageSize => Width * Height * 24;
    public override int RowStride { get; } = 0;
    public override int ElementStride { get; } = 0;
    public override bool CanEncode => true;

    public override bool CanResize => true;
    public override int WidthResizeIncrement => 1;
    public override int HeightResizeIncrement => 1;
    public override int DefaultWidth => 8;
    public override int DefaultHeight => 8;

    public Bmp24Codec()
    {
    }

    public Bmp24Codec(int width, int height) : base(width, height)
    {
    }

    public override ColorRgba32[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize)
            throw new ArgumentException(nameof(encodedBuffer));

        int src = 0;
        for (int y = el.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < el.Width; x++, src += 3)
            {
                var b = encodedBuffer[src];
                var g = encodedBuffer[src + 1];
                var r = encodedBuffer[src + 2];

                _nativeBuffer[y, x] = new ColorRgba32(r, g, b, 0xFF);
            }
        }

        return NativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, ColorRgba32[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        int dest = 0;
        for (int y = el.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < el.Width; x++, dest += 3)
            {
                var imageColor = imageBuffer[y, x];
                _foreignBuffer[dest] = imageColor.B;
                _foreignBuffer[dest + 1] = imageColor.G;
                _foreignBuffer[dest + 2] = imageColor.R;
            }
        }

        return _foreignBuffer;
    }
}
