using System;
using ImageMagitek.Codec;

namespace ImageMagitek.PluginSample;

public class Snes4bppCodec : IndexedCodec
{
    public override string Name => "SNES 4bpp Plugin";
    public override int Width { get; }
    public override int Height { get; }
    public override int StorageSize => 4 * Width * Height;
    public override ImageLayout Layout => ImageLayout.Tiled;
    public override int ColorDepth => 4;
    public override bool CanEncode => true;

    public override int DefaultWidth => 8;
    public override int DefaultHeight => 8;
    public override int WidthResizeIncrement => 1;
    public override int HeightResizeIncrement => 1;
    public override bool CanResize => true;

    private BitStream _bitStream;

    public Snes4bppCodec()
    {
        Width = DefaultWidth;
        Height = DefaultHeight;
        Initialize();
    }

    public Snes4bppCodec(int width, int height)
    {
        Width = width;
        Height = height;
        Initialize();
    }

    private void Initialize()
    {
        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new byte[Height, Width];
        _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    public override byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
            throw new ArgumentException(nameof(encodedBuffer));

        encodedBuffer.Slice(0, _foreignBuffer.Length).CopyTo(_foreignBuffer);

        _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);

        var offsetPlane1 = 0;
        var offsetPlane2 = Width;
        var offsetPlane3 = Width * Height * 2;
        var offsetPlane4 = Width * Height * 2 + Width;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _bitStream.SeekAbsolute(offsetPlane1);
                var bp1 = _bitStream.ReadBit();
                _bitStream.SeekAbsolute(offsetPlane2);
                var bp2 = _bitStream.ReadBit();
                _bitStream.SeekAbsolute(offsetPlane3);
                var bp3 = _bitStream.ReadBit();
                _bitStream.SeekAbsolute(offsetPlane4);
                var bp4 = _bitStream.ReadBit();

                var palIndex = (bp1 << 0) | (bp2 << 1) | (bp3 << 2) | (bp4 << 3);
                _nativeBuffer[y, x] = (byte)palIndex;

                offsetPlane1++;
                offsetPlane2++;
                offsetPlane3++;
                offsetPlane4++;
            }

            offsetPlane1 += Width;
            offsetPlane2 += Width;
            offsetPlane3 += Width;
            offsetPlane4 += Width;
        }

        return _nativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        var bs = BitStream.OpenWrite(StorageSize, 8);

        var offsetPlane1 = 0;
        var offsetPlane2 = Width;
        var offsetPlane3 = Width * Height * 2;
        var offsetPlane4 = Width * Height * 2 + Width;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var index = imageBuffer[y, x];

                byte bp1 = (byte)(index & 1);
                byte bp2 = (byte)((index >> 1) & 1);
                byte bp3 = (byte)((index >> 2) & 1);
                byte bp4 = (byte)((index >> 3) & 1);

                bs.SeekAbsolute(offsetPlane1);
                bs.WriteBit(bp1);
                bs.SeekAbsolute(offsetPlane2);
                bs.WriteBit(bp2);
                bs.SeekAbsolute(offsetPlane3);
                bs.WriteBit(bp3);
                bs.SeekAbsolute(offsetPlane4);
                bs.WriteBit(bp4);

                offsetPlane1++;
                offsetPlane2++;
                offsetPlane3++;
                offsetPlane3++;
            }
            offsetPlane1 += Width;
            offsetPlane2 += Width;
            offsetPlane3 += Width;
            offsetPlane4 += Width;
        }

        return bs.Data;
    }
}
