using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Codec
{
    public class IndexedPatternGraphicsCodec : IIndexedCodec
    {
        public string Name { get; set; }
        public FlowGraphicsFormat Format { get; private set; }
        public int StorageSize => Format.StorageSize;
        public ImageLayout Layout => Format.Layout;
        public PixelColorType ColorType => Format.ColorType;
        public int ColorDepth => Format.ColorDepth;
        public int Width => Format.Width;
        public int Height => Format.Height;
        public int RowStride => Format.RowStride;
        public int ElementStride => Format.ElementStride;

        protected byte[] _foreignBuffer;
        public virtual ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;

        protected byte[,] _nativeBuffer;
        public virtual byte[,] NativeBuffer => _nativeBuffer;

        public int DefaultWidth => Format.DefaultWidth;
        public int DefaultHeight => Format.DefaultHeight;
        public bool CanResize => !Format.FixedSize;
        public int WidthResizeIncrement { get; }
        public int HeightResizeIncrement => 1;


        public byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            throw new NotImplementedException();
        }

        public ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
        {
            throw new NotImplementedException();
        }

        public int GetPreferredWidth(int width) => DefaultWidth;
        public int GetPreferredHeight(int height) => DefaultHeight;

        public ReadOnlySpan<byte> ReadElement(in ArrangerElement el)
        {
            throw new NotImplementedException();
        }

        public void WriteElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            throw new NotImplementedException();
        }
    }
}
