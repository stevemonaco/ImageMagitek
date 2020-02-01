using ImageMagitek;
using ImageMagitek.Colors;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TileShop.WPF.Imaging
{
    public class IndexedImageSource : BitmapSourceBase
    {
        private IndexedImage _image;
        private Arranger _arranger;
        private Palette _defaultPalette;

        public IndexedImageSource(IndexedImage image, Arranger arranger, Palette defaultPalette)
        {
            _image = image;
            _arranger = arranger;
            _defaultPalette = defaultPalette;
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
                            var pal = _arranger.GetElementAtPixel(x, y).Palette ?? _defaultPalette;
                            var color = pal[row[x]];
                            pBytes[x * 4] = color.B;
                            pBytes[x * 4 + 1] = color.G;
                            pBytes[x * 4 + 2] = color.R;
                            pBytes[x * 4 + 3] = color.A;
                        }

                        pBytes += stride;
                    }
                }
            }
        }
    }
}
