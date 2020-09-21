using System;
using System.Buffers.Binary;
using System.Drawing;
using System.Windows.Media.Imaging;
using ImageMagitek;

namespace TileShop.WPF.Imaging
{
    public class IndexedBitmapAdapter : BitmapAdapter
    {
        public IndexedImage Image { get; }

        public IndexedBitmapAdapter(IndexedImage image)
        {
            Image = image;
            Width = Image.Width;
            Height = Image.Height;

            Bitmap = new WriteableBitmap(Width, Height, DpiX, DpiY, PixelFormat, null);
            Invalidate();
        }

        public override void Invalidate()
        {
            if (Image.Width != Bitmap.PixelWidth || Image.Height != Bitmap.PixelHeight)
            {
                Bitmap = new WriteableBitmap(Image.Width, Image.Height, DpiX, DpiY, PixelFormat, null);
                Width = Image.Width;
                Height = Image.Height;
            }

            Render(0, 0, Image.Width, Image.Height);
        }

        public override void Invalidate(Rectangle redrawRect)
        {
            var imageRect = new Rectangle(0, 0, Image.Width, Image.Height);
            var bitmapRect = new Rectangle(0, 0, Bitmap.PixelWidth, Bitmap.PixelHeight);

            if (imageRect.Contains(redrawRect) && bitmapRect.Contains(redrawRect))
            {
                Render(redrawRect.X, redrawRect.Y, redrawRect.Width, redrawRect.Height);
            }
            else
            {
                throw new ArgumentOutOfRangeException($"{nameof(Invalidate)}: Parameter '{nameof(redrawRect)}' {redrawRect} was not contained within '{nameof(Image)}' (0, 0, {Image.Width}, {Image.Height}) and '{nameof(Bitmap)}' (0, 0, {Bitmap.Width}, {Bitmap.Height})");
            }
        }

        protected override void Render(int x, int y, int width, int height)
        {
            try
            {
                if (!Bitmap.TryLock(new System.Windows.Duration(TimeSpan.FromMilliseconds(500))))
                    throw new TimeoutException($"{nameof(IndexedBitmapAdapter)}.{nameof(Render)} could not lock the Bitmap for rendering");

                unsafe
                {
                    for (int yDelta = 0; yDelta < height; yDelta++)
                    {
                        var destBuffer = (uint*)Bitmap.BackBuffer.ToPointer();
                        destBuffer += (y + yDelta) * Bitmap.BackBufferStride / 4 + x;

                        for (int xDelta = 0; xDelta < width; xDelta++)
                        {
                            var srcColor = Image.GetPixelColor(x + xDelta, y + yDelta);
                            uint destColor = (uint)(srcColor.B << 0) | (uint)(srcColor.G << 8) | (uint)(srcColor.R << 16) | (uint)(srcColor.A << 24);
                            *destBuffer = destColor;
                            destBuffer++;
                        }
                    }
                }

                Bitmap.AddDirtyRect(new System.Windows.Int32Rect(x, y, width, height));
            }
            finally
            {
                Bitmap.Unlock();
            }
        }
    }
}
