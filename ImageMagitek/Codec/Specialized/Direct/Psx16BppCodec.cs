using System;
using ImageMagitek.Colors;
using ImageMagitek.Colors.Converters;

namespace ImageMagitek.Codec;

public sealed class Psx16BppCodec : DirectCodec
{
    public override string Name => "PSX 16bpp";
    public override int Width { get; } = 8;
    public override int Height { get; } = 8;
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

    private readonly IBitStreamReader _bitReader;
    private readonly ColorConverterAbgr16 _colorConverter = new ColorConverterAbgr16();

    public Psx16BppCodec()
    {
        Width = DefaultWidth;
        Height = DefaultHeight;

        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new ColorRgba32[Height, Width];

        _bitReader = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    public Psx16BppCodec(int width, int height)
    {
        Width = width;
        Height = height;

        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new ColorRgba32[Height, Width];

        _bitReader = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    public override ColorRgba32[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize)
            throw new ArgumentException(nameof(encodedBuffer));

        encodedBuffer.Slice(0, _foreignBuffer.Length).CopyTo(_foreignBuffer);
        _bitReader.SeekAbsolute(0);

        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++)
            {
                uint packedColor = _bitReader.ReadByte();
                packedColor |= (uint)_bitReader.ReadByte() << 8;
                var abgr16 = new ColorAbgr16(packedColor);
                _nativeBuffer[y, x] = _colorConverter.ToNativeColor(abgr16);
            }
        }

        return NativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, ColorRgba32[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        var bs = BitStream.OpenWrite(StorageSize, 8);

        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x++)
            {
                var imageColor = imageBuffer[y, x];
                var fc = _colorConverter.ToForeignColor(imageColor);

                byte high = (byte)((fc.Color & 0xFF00) >> 8);
                byte low = (byte)(fc.Color & 0xFF);

                bs.WriteByte(low);
                bs.WriteByte(high);
            }
        }

        return bs.Data;
    }
}
