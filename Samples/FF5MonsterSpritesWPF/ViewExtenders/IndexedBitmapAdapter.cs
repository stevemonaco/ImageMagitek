using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ImageMagitek;
using ImageMagitek.Codec;

namespace FF5MonsterSprites.Imaging;

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
        if (Bitmap is null)
            throw new NullReferenceException($"{nameof(Invalidate)}: '{nameof(Bitmap)}' was null");

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
        if (Bitmap is null)
            throw new NullReferenceException($"{nameof(Invalidate)}: '{nameof(Bitmap)}' was null");

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
        if (Bitmap is null)
            throw new NullReferenceException($"{nameof(Invalidate)}: '{nameof(Bitmap)}' was null");

        try
        {
            if (!Bitmap.TryLock(new System.Windows.Duration(TimeSpan.FromMilliseconds(500))))
                throw new TimeoutException($"{nameof(IndexedBitmapAdapter)}.{nameof(Render)} could not lock the Bitmap for rendering");

            unsafe
            {
                var backBuffer = (uint*)Bitmap.BackBuffer.ToPointer();
                var stride = Bitmap.BackBufferStride;

                Parallel.For(yStart, yStart + height - 1, (scanline) =>
                {
                    var dest = backBuffer + scanline * stride / 4 + xStart;
                    var src = Image.GetPixelRowSpan(scanline);

                    for (int x = 0; x < width; x++)
                    {
                        dest[x] = TranslateColor(x, scanline, src);
                    }
                });
            }

            Bitmap.AddDirtyRect(new System.Windows.Int32Rect(xStart, yStart, width, height));
        }
        finally
        {
            Bitmap.Unlock();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint TranslateColor(int x, int y, Span<byte> sourceRow)
    {
        uint outputColor = 0;

        var el = Image.GetElementAtPixel(x, y);

        if (el?.Codec is IIndexedCodec codec)
        {
            var pal = codec.Palette;
            var index = sourceRow[x];
            var inputColor = pal[index].Color;

            outputColor = (inputColor & 0xFF00FF00) | BitOperations.RotateLeft(inputColor & 0xFF00FF, 16);

            if (index == 0 && pal.ZeroIndexTransparent)
                outputColor &= 0x00FFFFFF;
        }

        return outputColor;
    }
}
