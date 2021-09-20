using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec
{
    public sealed class Psx24bppCodec : DirectCodec
    {
        public override string Name => "PSX 24bpp";
        public override int Width { get; } = 8;
        public override int Height { get; } = 8;
        public override ImageLayout Layout => ImageLayout.Single;
        public override int ColorDepth => 24;
        public override int StorageSize => Width * Height * 24;
        public override bool CanEncode => true;

        public override int RowStride => 0;
        public override int ElementStride => 0;
        public override bool CanResize => true;
        public override int WidthResizeIncrement => 1;
        public override int HeightResizeIncrement => 1;
        public override int DefaultWidth => 64;
        public override int DefaultHeight => 64;

        private BitStream _bitStream;

        public Psx24bppCodec()
        {
            Width = DefaultWidth;
            Height = DefaultHeight;

            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new ColorRgba32[Height, Width];

            _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
        }

        public Psx24bppCodec(int width, int height)
        {
            Width = width;
            Height = height;

            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new ColorRgba32[Height, Width];

            _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
        }

        public override ColorRgba32[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            if (encodedBuffer.Length * 8 < StorageSize)
                throw new ArgumentException(nameof(encodedBuffer));

            encodedBuffer.Slice(0, _foreignBuffer.Length).CopyTo(_foreignBuffer);
            _bitStream.SeekAbsolute(0);

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    byte r = _bitStream.ReadByte();
                    byte g = _bitStream.ReadByte();
                    byte b = _bitStream.ReadByte();

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

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    var imageColor = imageBuffer[y, x];
                    bs.WriteByte(imageColor.R);
                    bs.WriteByte(imageColor.G);
                    bs.WriteByte(imageColor.B);
                }
            }

            return bs.Data;
        }
    }
}
