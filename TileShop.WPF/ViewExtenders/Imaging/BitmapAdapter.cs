using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Stylet;

namespace TileShop.WPF.Imaging
{
    public abstract class BitmapAdapter : PropertyChangedBase
    {
        private WriteableBitmap _bitmap;
        public WriteableBitmap Bitmap
        {
            get => _bitmap;
            protected set => SetAndNotify(ref _bitmap, value);
        }

        private int _width;
        public int Width
        {
            get => _width;
            protected set => SetAndNotify(ref _width, value);
        }

        private int _height;
        public int Height
        {
            get => _height;
            protected set => SetAndNotify(ref _height, value);
        }

        public int DpiX { get; protected set; } = 96;
        public int DpiY { get; protected set; } = 96;
        public PixelFormat PixelFormat { get; protected set; } = PixelFormats.Bgra32;

        public abstract void Invalidate();
        public abstract void Invalidate(Rectangle redrawRect);
        protected abstract void Render(int x, int y, int width, int height);
    }
}
