using System;
using System.Linq;

namespace ImageMagitek.Codec
{
    public sealed class BlankIndexedCodec : IndexedCodec
    {
        public override string Name => "Blank Indexed";
        public override int Width { get; }
        public override int Height { get; }
        public override ImageLayout Layout => ImageLayout.Tiled;
        public override int ColorDepth => 0;
        public override int StorageSize => 0;

        public override int DefaultWidth => 8;
        public override int DefaultHeight => 8;
        public override int RowStride => 0;
        public override int ElementStride => 0;
        public override int WidthResizeIncrement => 1;
        public override int HeightResizeIncrement => 1;
        public override bool CanResize => true;

        private byte _fillIndex = 0;

        public BlankIndexedCodec()
        {
            _foreignBuffer = Enumerable.Empty<byte>().ToArray();
        }

        public override byte[,] DecodeElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            if (_nativeBuffer?.GetLength(0) != el.Width || _nativeBuffer?.GetLength(1) != el.Height)
            {
                _nativeBuffer = new byte[el.Width, el.Height];

                for (int y = 0; y < el.Height; y++)
                    for (int x = 0; x < el.Width; x++)
                        _nativeBuffer[x, y] = _fillIndex;
            }

            return NativeBuffer;
        }

        public override ReadOnlySpan<byte> EncodeElement(ArrangerElement el, byte[,] imageBuffer) => ForeignBuffer;
        public override ReadOnlySpan<byte> ReadElement(ArrangerElement el) => ForeignBuffer;
        public override void WriteElement(ArrangerElement el, ReadOnlySpan<byte> encodedBuffer) { }
    }
}
