using System;
using ImageMagitek.Colors;

namespace ImageMagitek.Codec
{
    public class Rgb24TiledCodec : DirectCodec
    {
        public override string Name => "Rgb24 Tiled";
        public override int Width { get; } = 8;
        public override int Height { get; } = 8;
        public override ImageLayout Layout => ImageLayout.Tiled;
        public override int ColorDepth => 24;
        public override int StorageSize => Width * Height * 24;
        public override int RowStride { get; } = 0;
        public override int ElementStride { get; } = 0;

        public override bool CanResize => true;
        public override int WidthResizeIncrement => 1;
        public override int HeightResizeIncrement => 1;
        public override int DefaultWidth => 8;
        public override int DefaultHeight => 8;

        private BitStream _bitStream;

        public Rgb24TiledCodec()
        {
            Width = DefaultWidth;
            Height = DefaultHeight;

            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new ColorRgba32[Height, Width];

            _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
        }

        public Rgb24TiledCodec(int width, int height)
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
                    var r = _bitStream.ReadByte();
                    var g = _bitStream.ReadByte();
                    var b = _bitStream.ReadByte();

                    _nativeBuffer[y, x] = new ColorRgba32(r, g, b, 0xFF);
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
