using System;
using ImageMagitek.Codec;

namespace ImageMagitek.PluginSamples;

/// <summary>
/// In-game, this font is a 8px wide, variable height tile
/// The font location is 0x25005
/// This codec is view-only and does not support editing.
/// </summary>
public class LastArmageddonCodec : IndexedCodec
{
    public override string Name => "Last Armageddon Font";
    public override ImageLayout Layout => ImageLayout.Single;
    public override int ColorDepth => 1;
    public override int StorageSize => 0x3000; // 0x5D8;
    public override bool CanEncode => false;

    public override int DefaultWidth => 8 * 32;
    public override int DefaultHeight => 8;
    public override int WidthResizeIncrement => 0;
    public override int HeightResizeIncrement => 0;
    public override bool CanResize => false;

    private IBitStreamReader _bitReader;

    public LastArmageddonCodec() : base(8 * 32, 8)
    {
        _bitReader = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    private void Initialize()
    {
        _foreignBuffer = new byte[(StorageSize + 7) / 8];
        _nativeBuffer = new byte[Height, Width];
        _bitReader = BitStream.OpenRead(_foreignBuffer, StorageSize);
    }

    public override byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
    {
        if (encodedBuffer.Length * 8 < StorageSize)
            throw new ArgumentException($"{nameof(DecodeElement)}: buffer size is too small", nameof(encodedBuffer));

        encodedBuffer[.._foreignBuffer.Length].CopyTo(_foreignBuffer);

        _bitReader = BitStream.OpenRead(_foreignBuffer, StorageSize);

        try
        {
            int n = 32;
            for (int i = 0; i < n; i++)
            {
                int height = _bitReader.ReadByte();
                int unk = _bitReader.ReadByte();

                int yPos = 7;

                while (height > 0)
                {
                    if ((height & 0x1) == 0)
                    {
                        _nativeBuffer[yPos, i * 8 + 7] = (byte)_bitReader.ReadBit();
                        _nativeBuffer[yPos, i * 8 + 6] = (byte)_bitReader.ReadBit();
                        _nativeBuffer[yPos, i * 8 + 5] = (byte)_bitReader.ReadBit();
                        _nativeBuffer[yPos, i * 8 + 4] = (byte)_bitReader.ReadBit();
                        _nativeBuffer[yPos, i * 8 + 3] = (byte)_bitReader.ReadBit();
                        _nativeBuffer[yPos, i * 8 + 2] = (byte)_bitReader.ReadBit();
                        _nativeBuffer[yPos, i * 8 + 1] = (byte)_bitReader.ReadBit();
                        _nativeBuffer[yPos, i * 8 + 0] = (byte)_bitReader.ReadBit();
                    }

                    yPos--;
                    height >>= 1;
                }

                //int yPos = 7;
                //for (int y = 0; y < 8; y++)
                //{
                //    if ((height & 0x80) != 0)
                //        _nativeBuffer[y, i * 8 + 7] = (byte)_bitReader.ReadBit();
                //    if ((height & 0x40) != 0)
                //        _nativeBuffer[y, i * 8 + 6] = (byte)_bitReader.ReadBit();
                //    if ((height & 0x20) != 0)
                //        _nativeBuffer[y, i * 8 + 5] = (byte)_bitReader.ReadBit();
                //    if ((height & 0x10) != 0)
                //        _nativeBuffer[y, i * 8 + 4] = (byte)_bitReader.ReadBit();
                //    if ((height & 0x08) != 0)
                //        _nativeBuffer[y, i * 8 + 3] = (byte)_bitReader.ReadBit();
                //    if ((height & 0x04) != 0)
                //        _nativeBuffer[y, i * 8 + 2] = (byte)_bitReader.ReadBit();
                //    if ((height & 0x02) != 0)
                //        _nativeBuffer[y, i * 8 + 1] = (byte)_bitReader.ReadBit();
                //    if ((height & 0x01) != 0)
                //        _nativeBuffer[y, i * 8 + 0] = (byte)_bitReader.ReadBit();
                //}
            }
        }
        catch (Exception)
        {

        }

        return _nativeBuffer;
    }

    public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
    {
        throw new NotSupportedException($"'{Name}' is a read-only codec");
    }
}
