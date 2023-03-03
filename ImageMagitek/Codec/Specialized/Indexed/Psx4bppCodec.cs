using System;

namespace ImageMagitek.Codec;
public sealed class Psx4bppCodec : IndexedCodec
{
    public override string Name => "PSX 4bpp";
    public override ImageLayout Layout => ImageLayout.Single;
    public override int ColorDepth => 4;
    public override int StorageSize => Width * Height * 4;
    public override bool CanEncode => true;

    public override int DefaultWidth => 64;
    public override int DefaultHeight => 64;
    public override int WidthResizeIncrement => 2;
    public override int HeightResizeIncrement => 1;
    public override bool CanResize => true;

    private IBitStreamReader _bitReader;

    public Psx4bppCodec()
    {
        _bitReader = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    public Psx4bppCodec(int width, int height) : base(width, height)
    {
        _bitReader = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    public override byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
            throw new ArgumentException(nameof(encodedBuffer));

        encodedBuffer[..ForeignBuffer.Length].CopyTo(_foreignBuffer);

        _bitReader.SeekAbsolute(0);

        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x += 2)
            {
                var palIndex = (byte)_bitReader.ReadBits(4);
                _nativeBuffer[y, x + 1] = palIndex;

                palIndex = (byte)_bitReader.ReadBits(4);
                _nativeBuffer[y, x] = palIndex;
            }
        }

        return _nativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
    {
        if (imageBuffer.GetLength(0) != Height || imageBuffer.GetLength(1) != Width)
            throw new ArgumentException(nameof(imageBuffer));

        int dest = 0;
        for (int y = 0; y < el.Height; y++)
        {
            for (int x = 0; x < el.Width; x += 2, dest++)
            {
                byte indexLow = imageBuffer[y, x];
                byte indexHigh = imageBuffer[y, x + 1];

                byte index = (byte)(indexLow | (indexHigh << 4));
                _foreignBuffer[dest] = index;
            }
        }

        return ForeignBuffer;
    }
}
