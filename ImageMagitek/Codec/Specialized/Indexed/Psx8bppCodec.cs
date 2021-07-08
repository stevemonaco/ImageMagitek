using System;

namespace ImageMagitek.Codec
{
    public sealed class Psx8bppCodec : IndexedCodec
    {
        public override string Name => "PSX 8bpp";
        public override int Width { get; }
        public override int Height { get; }
        public override ImageLayout Layout => ImageLayout.Single;
        public override int ColorDepth => 8;
        public override int StorageSize => Width * Height * 8;

        public override int DefaultWidth => 64;
        public override int DefaultHeight => 64;
        public override int WidthResizeIncrement => 1;
        public override int HeightResizeIncrement => 1;
        public override bool CanResize => true;

        public Psx8bppCodec()
        {
            Width = DefaultWidth;
            Height = DefaultHeight;
            Initialize();
        }

        public Psx8bppCodec(int width, int height)
        {
            Width = width;
            Height = height;
            Initialize();
        }

        private void Initialize()
        {
            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new byte[Height, Width];
        }

        public override byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
                throw new ArgumentException(nameof(encodedBuffer));

            int srcidx = 0;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++, srcidx++)
                {
                    var palIndex = encodedBuffer[srcidx];
                    _nativeBuffer[y, x] = palIndex;
                }
            }

            return _nativeBuffer;
        }

        public override ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
        {
            if (imageBuffer.GetLength(0) != Width || imageBuffer.GetLength(1) != Height)
                throw new ArgumentException(nameof(imageBuffer));

            int destidx = 0;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++, destidx++)
                {
                    byte index = imageBuffer[y, x];
                    _foreignBuffer[destidx] = index;
                }
            }

            return ForeignBuffer;
        }
    }
}
