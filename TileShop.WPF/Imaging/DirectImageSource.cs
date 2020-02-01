using ImageMagitek;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TileShop.WPF.Imaging
{
    public class DirectImageSource : BitmapSourceBase
    {
        private DirectImage _image;

        public DirectImageSource(DirectImage image)
        {
            _image = image;
        }

        protected override Freezable CreateInstanceCore()
        {
            return new DirectImageSource(null);
        }

        public override PixelFormat Format => PixelFormats.Bgra32;
        public override int PixelHeight => _image.Height;
        public override int PixelWidth => _image.Width;
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
