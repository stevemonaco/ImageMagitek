using ImageMagitek.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Codec
{
    public class IndexedPatternGraphicsCodec : IIndexedCodec
    {
        public string Name { get; set; }
        public PatternGraphicsFormat Format { get; private set; }
        public int StorageSize => Format.StorageSize;
        public ImageLayout Layout => Format.Layout;
        public PixelColorType ColorType => Format.ColorType;
        public int ColorDepth => Format.ColorDepth;
        public int Width => Format.Width;
        public int Height => Format.Height;

        protected byte[] _foreignBuffer;
        public virtual ReadOnlySpan<byte> ForeignBuffer => _foreignBuffer;

        protected byte[,] _nativeBuffer;
        public virtual byte[,] NativeBuffer => _nativeBuffer;

        private BitStream _bitStream;
        private int[] _planeOffsets;
        private List<int[,]> _planeImages;

        public int DefaultWidth => Format.DefaultWidth;
        public int DefaultHeight => Format.DefaultHeight;
        public bool CanResize => !Format.FixedSize;
        public int WidthResizeIncrement { get; }
        public int HeightResizeIncrement => 1;

        public IndexedPatternGraphicsCodec(PatternGraphicsFormat format)
        {
            Format = format;
            Name = format.Name;
            AllocateBuffers();
        }

        public byte[,] DecodeElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            if (encodedBuffer.Length * 8 < StorageSize) // Decoding would require data past the end of the buffer
                throw new ArgumentException(nameof(encodedBuffer));

            encodedBuffer.Slice(0, _foreignBuffer.Length).CopyTo(_foreignBuffer);
            _bitStream.SeekAbsolute(0);

            for (int i = 0; i < StorageSize; i++)
            {
                var bit = _bitStream.ReadBit();
                var index = Format.Pattern.GetDecodeIndex(i);
                var plane = index / (StorageSize / Format.ColorDepth);
                var pixel = index % (StorageSize / Format.ColorDepth);
                var x = pixel % Width;
                var y = pixel / Width;
                _planeImages[plane][x, y] = bit;
            }

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int color = 0;
                    for (int i = 0; i < Format.ColorDepth; i++)
                    {
                        color |= _planeImages[i][x, y] << i;
                    }
                    NativeBuffer[x, y] = (byte)color;
                }
            }

            return NativeBuffer;
        }

        public ReadOnlySpan<byte> EncodeElement(in ArrangerElement el, byte[,] imageBuffer)
        {
            throw new NotImplementedException();
        }

        private void AllocateBuffers()
        {
            _foreignBuffer = new byte[(StorageSize + 7) / 8];
            _nativeBuffer = new byte[Width, Height];

            _bitStream = BitStream.OpenRead(_foreignBuffer, StorageSize);
            _planeOffsets = new int[Format.ColorDepth];

            _planeImages = new List<int[,]>();
            for (int i = 0; i < Format.ColorDepth; i++)
                _planeImages.Add(new int[Width, Height]);
        }

        public int GetPreferredWidth(int width) => DefaultWidth;
        public int GetPreferredHeight(int height) => DefaultHeight;

        public ReadOnlySpan<byte> ReadElement(in ArrangerElement el)
        {
            var buffer = new byte[(StorageSize + 7) / 8];
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8)
                return null;

            fs.ReadShifted(el.FileAddress, StorageSize, buffer);

            return buffer;
        }

        public void WriteElement(in ArrangerElement el, ReadOnlySpan<byte> encodedBuffer)
        {
            var fs = el.DataFile.Stream;
            fs.WriteShifted(el.FileAddress, StorageSize, encodedBuffer);
        }
    }
}
