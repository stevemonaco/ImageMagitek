using System;
using System.Drawing;
using System.Windows.Media.Imaging;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

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

        /// <summary>
        /// Invalidates and redraws the entirety of the Bitmap
        /// </summary>
        public override void Invalidate()
        {
            Render(0, 0, Width, Height);
        }

        /// <summary>
        /// Invalidates and redraws a subregion of the Bitmap
        /// </summary>
        /// <param name="redrawRect"></param>
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

        /// <summary>
        /// Invalidates and redraws a region of the Bitmap
        /// </summary>
        /// <param name="x">Left coordinate in pixel coordinates</param>
        /// <param name="y">Top coordinate in pixel coordinates</param>
        /// <param name="width">Width of region</param>
        /// <param name="height">Height of region</param>
        public override void Invalidate(int x, int y, int width, int height)
        {
            var imageRect = new Rectangle(0, 0, Image.Width, Image.Height);
            var bitmapRect = new Rectangle(0, 0, Bitmap.PixelWidth, Bitmap.PixelHeight);
            var redrawRect = new Rectangle(x, y, width, height);

            if (imageRect.Contains(redrawRect) && bitmapRect.Contains(redrawRect))
            {
                Render(x, y, width, height);
            }
            else
            {
                throw new ArgumentOutOfRangeException($"{nameof(Invalidate)}: Parameter '{nameof(redrawRect)}' {redrawRect} was not contained within '{nameof(Image)}' (0, 0, {Image.Width}, {Image.Height}) and '{nameof(Bitmap)}' (0, 0, {Bitmap.Width}, {Bitmap.Height})");
            }
        }

        protected override void Render(int xStart, int yStart, int width, int height)
        {
            try
            {
                if (!Bitmap.TryLock(new System.Windows.Duration(TimeSpan.FromMilliseconds(500))))
                    throw new TimeoutException($"{nameof(IndexedBitmapAdapter)}.{nameof(Render)} could not lock the Bitmap for rendering");

                unsafe
                {
                    for (int y = yStart; y < yStart + height; y++)
                    {
                        var dest = (byte*)Bitmap.BackBuffer.ToPointer();
                        dest += y * Bitmap.BackBufferStride + xStart * 4;
                        var src = Image.GetPixelRowSpan(y);

                        for (int x = 0; x < width; x++)
                        {
                            var el = Image.GetElementAtPixel(x + xStart, y);

                            // TODO: More elegant handling of element fallback to clearing

                            if (el is ArrangerElement element)
                            {
                                var pal = element.Palette;

                                if (pal is object)
                                {
                                    var index = src[x + xStart];
                                    var color = pal[index];

                                    dest[x * 4] = color.B;
                                    dest[x * 4 + 1] = color.G;
                                    dest[x * 4 + 2] = color.R;

                                    if (index == 0 && pal.ZeroIndexTransparent)
                                        dest[x * 4 + 3] = 0;
                                    else
                                        dest[x * 4 + 3] = color.A;
                                }
                            }
                            else
                            {
                                dest[x * 4] = 0;
                                dest[x * 4 + 1] = 0;
                                dest[x * 4 + 2] = 0;
                                dest[x * 4 + 3] = 0;
                            }
                        }
                    }
                }

                Bitmap.AddDirtyRect(new System.Windows.Int32Rect(xStart, yStart, width, height));
            }
            finally
            {
                Bitmap.Unlock();
            }
        }
    }
}
