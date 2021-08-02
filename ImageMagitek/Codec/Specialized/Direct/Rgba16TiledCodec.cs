using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec
{
    public class Rgba16TiledCodec : DirectCodec
    {
        public override string Name => "Rgb24 Tiled";
        public override int Width { get; } = 8;
        public override int Height { get; } = 8;
        public override ImageLayout Layout => ImageLayout.Tiled;
        public override int ColorDepth => 32;
        public override int StorageSize => Width * Height * 32;
        public override int RowStride { get; } = 0;
        public override int ElementStride { get; } = 0;

        public override bool CanResize => true;
        public override int WidthResizeIncrement => 1;
        public override int HeightResizeIncrement => 1;
        public override int DefaultWidth => 8;
        public override int DefaultHeight => 8;

        private BitStream _bitStream;

        public Rgba16TiledCodec()
        {
            Width = DefaultWidth;
            Height = DefaultHeight;

            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new ColorRgba32[Height, Width];

            _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
        }

        public Rgba16TiledCodec(int width, int height)
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
                    ushort pair = (ushort)(_bitStream.ReadByte() << 8 | _bitStream.ReadByte());
                    byte r = (byte)((pair >> 11) << 3);
                    byte g = (byte)(((pair >> 6) & 0x1F) << 3);
                    byte b = (byte)(((pair >> 1) & 0x1F) << 3);
                    byte a = (pair & 0x1) == 1 ? (byte)0xFF : (byte)0;

                    _nativeBuffer[y, x] = new ColorRgba32(r, g, b, a);
                }
            }

            return NativeBuffer;
        }

        public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, ColorRgba32[,] imageBuffer)
        {
            throw new NotImplementedException();
        }
    }
}
