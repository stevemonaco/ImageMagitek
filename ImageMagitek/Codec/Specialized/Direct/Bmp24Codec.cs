using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec;

public sealed class Bmp24Codec : DirectCodec
{
    public override string Name => "Bmp24";
    public override int Width { get; } = 8;
    public override int Height { get; } = 8;
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

    private IBitStreamReader _bitReader;

    public Bmp24Codec()
    {
        _bitReader = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    public Bmp24Codec(int width, int height) : base(width, height)
    {
        _bitReader = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    public override ColorRgba32[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize)
            throw new ArgumentException(nameof(encodedBuffer));

        encodedBuffer[.._foreignBuffer.Length].CopyTo(_foreignBuffer);
        _bitReader.SeekAbsolute(0);

        for (int y = el.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < el.Width; x++)
            {
                var b = _bitReader.ReadByte();
                var g = _bitReader.ReadByte();
                var r = _bitReader.ReadByte();

                _nativeBuffer[y, x] = new ColorRgba32(r, g, b, 0xFF);
            }
        }

        return NativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, ColorRgba32[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        var bs = BitStream.OpenWrite(StorageSize, 8);

        for (int y = el.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < el.Width; x++)
            {
                var imageColor = imageBuffer[y, x];
                bs.WriteByte(imageColor.B);
                bs.WriteByte(imageColor.G);
                bs.WriteByte(imageColor.R);
            }
        }

        return bs.Data;
    }
}
