using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagitek;

namespace TileShop.WPF.Imaging
{
    public class DirectImageSource : ArrangerBitmapSource
    {
        private DirectImage _image;

        public DirectImageSource(DirectImage image) : this(image, 0, 0, image.Width, image.Height)
        {
        }

        public DirectImageSource(DirectImage image, int x, int y, int width, int height)
        {
            _image = image;
            PixelWidth = width;
            PixelHeight = height;
            CropX = x;
            CropY = y;
        }

        protected override Freezable CreateInstanceCore() => new DirectImageSource(null);

        public override PixelFormat Format => PixelFormats.Bgra32;
        public override int PixelWidth { get; }
        public override int PixelHeight { get; }
        public override double DpiX => 96;
        public override double DpiY => 96;
        public override BitmapPalette Palette => null;

        protected override void CopyPixelsCore(Int32Rect sourceRect, int stride, int bufferSize, IntPtr buffer)
        {
            if (_image is object)
            {
                unsafe
                {
                    byte* pBytes = (byte*)buffer.ToPointer();
                    for (int y = 0; y < sourceRect.Height; y++)
                    {
                        var row = _image.GetPixelRowSpan(y);

                        for (int x = 0; x < sourceRect.Width; x++)
                        {
                            pBytes[x * 4] = row[x].B;
                            pBytes[x * 4 + 1] = row[x].G;
                            pBytes[x * 4 + 2] = row[x].R;
                            pBytes[x * 4 + 3] = row[x].A;
                        }

                        pBytes += stride;
                    }
                }
            }
        }
    }
}
