using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;
using SixLabors.ImageSharp.Advanced;
using System;

namespace ImageMagitek.Codec
{
    public sealed class Snes3bppCodec : IGraphicsCodec
    {
        public string Name => "SNES 3bpp";
        public int Width { get; private set; } = 8;
        public int Height { get; private set; } = 8;
        public int StorageSize => 3 * Width * Height;
        public ImageLayout Layout => ImageLayout.Tiled;
        public PixelColorType ColorType => PixelColorType.Indexed;
        public int ColorDepth => 3;
        public int RowStride => 0;
        public int ElementStride => 0;
        public Palette DefaultPalette { get; set; }

        private byte[] _buffer;
        private Memory<byte> _memoryBuffer;
        private BitStream _bitStream;

        public Snes3bppCodec(int width, int height, Palette defaultPalette)
        {
            Width = width;
            Height = height;
            DefaultPalette = defaultPalette;

            _buffer = new byte[(StorageSize + 7) / 8];
            _memoryBuffer = new Memory<byte>(_buffer);
            _bitStream = BitStream.OpenRead(_buffer, StorageSize);
        }

        public void Decode(Image<Rgba32> image, ArrangerElement el)
        {
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8) // Element would contain data past the end of the file
                return;

            var dest = image.GetPixelSpan();
            int destidx = image.Width * el.Y1 + el.X1;

            _bitStream.SeekAbsolute(0);
            fs.ReadUnshifted(el.FileAddress, StorageSize, true, _memoryBuffer.Span);

            var pal = el.Palette ?? DefaultPalette;

            var offsetPlane1 = 0;
            var offsetPlane2 = el.Width;
            var offsetPlane3 = el.Width * el.Height * 2;

            for(int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    _bitStream.SeekAbsolute(offsetPlane1);
                    var bp1 = _bitStream.ReadBit();
                    _bitStream.SeekAbsolute(offsetPlane2);
                    var bp2 = _bitStream.ReadBit();
                    _bitStream.SeekAbsolute(offsetPlane3);
                    var bp3 = _bitStream.ReadBit();

                    var palIndex = (bp1 << 0) | (bp2 << 1) | (bp3 << 2);
                    dest[destidx] = pal[palIndex].ToRgba32();
                    destidx++;

                    offsetPlane1++;
                    offsetPlane2++;
                    offsetPlane3++;
                }

                destidx += el.X1 + image.Width - (el.X2 + 1);
                offsetPlane1 += Width;
                offsetPlane2 += Width;
            }
        }

        public void Encode(Image<Rgba32> image, ArrangerElement el)
        {
            var fs = el.DataFile.Stream;

            if (el.FileAddress + StorageSize > fs.Length * 8) // Ensure there is enough data to decode
                return;

            var bs = BitStream.OpenWrite(StorageSize, 8);

            var offsetPlane1 = 0;
            var offsetPlane2 = el.Width;
            var offsetPlane3 = el.Width * el.Height * 2;

            var pal = el.Palette ?? DefaultPalette;

            for (int y = 0; y < el.Height; y++)
            {
                for (int x = 0; x < el.Width; x++)
                {
                    var imageColor = image[x + el.X1, y + el.Y1];
                    var nc = new ColorRgba32(imageColor.R, imageColor.G, imageColor.B, imageColor.A);
                    byte index = pal.GetIndexByNativeColor(nc, true);

                    byte bp1 = (byte)(index & 1);
                    byte bp2 = (byte)((index >> 1) & 1);
                    byte bp3 = (byte)((index >> 2) & 1);

                    bs.SeekAbsolute(offsetPlane1);
                    bs.WriteBit(bp1);
                    bs.SeekAbsolute(offsetPlane2);
                    bs.WriteBit(bp2);
                    bs.SeekAbsolute(offsetPlane3);
                    bs.WriteBit(bp3);

                    offsetPlane1++;
                    offsetPlane2++;
                    offsetPlane3++;
                }
                offsetPlane1 += Width;
                offsetPlane2 += Width;
            }

            fs.Seek(el.FileAddress.FileOffset, System.IO.SeekOrigin.Begin);
            fs.Write(bs.Data, 0, bs.Data.Length);
        }
    }
}
