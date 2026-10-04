using System;
using System.Buffers.Binary;
using ImageMagitek.Colors;
using ImageMagitek.Colors.Converters;

namespace ImageMagitek.Codec;

public sealed class Psx16BppCodec : DirectCodec
{
    public override string Name => "PSX 16bpp";
    public override ImageLayout Layout => ImageLayout.Single;
    public override int ColorDepth => 16;
    public override int StorageSize => Width * Height * 16;
    public override bool CanEncode => true;

    public override int RowStride => 0;
    public override int ElementStride => 0;
    public override bool CanResize => true;
    public override int WidthResizeIncrement => 1;
    public override int HeightResizeIncrement => 1;
    public override int DefaultWidth => 64;
    public override int DefaultHeight => 64;

    private readonly ColorConverterAbgr16 _colorConverter = new ColorConverterAbgr16();

    public Psx16BppCodec()
    {
    }

    public Psx16BppCodec(int width, int height) : base(width, height)
    {
    }

    public override ColorRgba32[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize)
            throw new ArgumentException(nameof(encodedBuffer));

        int src = 0;
        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++, src += 2)
            {
                var abgr16 = new ColorAbgr16(BinaryPrimitives.ReadUInt16LittleEndian(encodedBuffer[src..]));
                _nativeBuffer[y, x] = _colorConverter.ToNativeColor(abgr16);
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
            for (int x = 0; x < el.Width; x++, dest += 2)
            {
                var imageColor = imageBuffer[y, x];
                var fc = _colorConverter.ToForeignColor(imageColor);
                BinaryPrimitives.WriteUInt16LittleEndian(_foreignBuffer.AsSpan(dest), (ushort)fc.Color);
            }
        }

        return _foreignBuffer;
    }
}
