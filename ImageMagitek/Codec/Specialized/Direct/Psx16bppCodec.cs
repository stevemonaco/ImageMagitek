using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec
{
    public class Psx16bppCodec : DirectCodec
    {
        public override string Name => "PSX 16bpp";
        public override int Width { get; } = 8;
        public override int Height { get; } = 8;
        public override ImageLayout Layout => ImageLayout.Single;
        public override int ColorDepth => 16;
        public override int StorageSize => Width * Height * 16;

        private BitStream _bitStream;

        public Psx16bppCodec(int width, int height)
        {
            Width = width;
            Height = height;

            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new ColorRgba32[Width, Height];

            _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
        }

        public override ColorRgba32[,] DecodeElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            if (encodedBuffer.Length * 8 < StorageSize)
                throw new ArgumentException(nameof(encodedBuffer));

            encodedBuffer.Slice(0, _foreignBuffer.Length).CopyTo(_foreignBuffer);
            _bitStream.SeekAbsolute(0);

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    uint packedColor = _bitStream.ReadByte();
                    packedColor |= (uint)_bitStream.ReadByte() << 8;
                    var colorAbgr16 = ColorFactory.CreateColor(ColorModel.ABGR16, packedColor);
                    _nativeBuffer[x, y] = ColorConverter.ToNative(colorAbgr16);
                }
            }

            return NativeBuffer;
        }

        public override ReadOnlySpan<byte> EncodeElement(ArrangerElement el, ColorRgba32[,] imageBuffer)
        {
            if (imageBuffer.GetLength(0) != Width || imageBuffer.GetLength(1) != Height)
                throw new ArgumentException(nameof(imageBuffer));

            var bs = BitStream.OpenWrite(StorageSize, 8);

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    var imageColor = imageBuffer[x, y];
                    var fc = ColorConverter.ToForeign(imageColor, ColorModel.ABGR16);

                    byte high = (byte)(fc.Color & 0xFF00);
                    byte low = (byte)(fc.Color & 0xFF);

                    bs.WriteByte(low);
                    bs.WriteByte(high);
                }
            }

            return bs.Data;
        }
    }
}
