﻿using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using ImageMagitek;
using ImageMagitek.Colors;

namespace TileShop.UI.Imaging;

public class DirectBitmapAdapter : BitmapAdapter
{
    public DirectImage Image { get; }

    public DirectBitmapAdapter(DirectImage image)
    {
        Image = image;
        Width = Image.Width;
        Height = Image.Height;

        Bitmap = new WriteableBitmap(new PixelSize(Width, Height), new Avalonia.Vector(DpiX, DpiY), PixelFormat, AlphaFormat.Unpremul);
        Invalidate();
    }

    /// <summary>
    /// Invalidates and redraws the entirety of the Bitmap
    /// </summary>
    public override void Invalidate()
    {
        Render(0, 0, Image.Width, Image.Height);
    }

    /// <summary>
    /// Invalidates and redraws a subregion of the Bitmap
    /// </summary>
    /// <param name="redrawRect"></param>
    public override void Invalidate(Rectangle redrawRect)
    {
        var imageRect = new Rectangle(0, 0, Image.Width, Image.Height);
        var bitmapRect = new Rectangle(0, 0, Bitmap.PixelSize.Width, Bitmap.PixelSize.Height);

        if (imageRect.Contains(redrawRect) && bitmapRect.Contains(redrawRect))
        {
            Render(redrawRect.X, redrawRect.Y, redrawRect.Width, redrawRect.Height);
        }
        else
        {
            throw new ArgumentOutOfRangeException($"{nameof(Invalidate)}: Parameter '{nameof(redrawRect)}' {redrawRect} was not contained within '{nameof(Image)}' (0, 0, {Image.Width}, {Image.Height}) and '{nameof(Bitmap)}' (0, 0, {Bitmap.PixelSize.Width}, {Bitmap.PixelSize.Height})");
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
        var bitmapRect = new Rectangle(0, 0, Bitmap.PixelSize.Width, Bitmap.PixelSize.Height);
        var redrawRect = new Rectangle(x, y, width, height);

        if (imageRect.Contains(redrawRect) && bitmapRect.Contains(redrawRect))
        {
            Render(x, y, width, height);
        }
        else
        {
            throw new ArgumentOutOfRangeException($"{nameof(Invalidate)}: Parameter '{nameof(redrawRect)}' {redrawRect} was not contained within '{nameof(Image)}' (0, 0, {Image.Width}, {Image.Height}) and '{nameof(Bitmap)}' (0, 0, {Bitmap.PixelSize.Width}, {Bitmap.PixelSize.Height})");
        }
    }

    protected override void Render(int xStart, int yStart, int width, int height)
    {
        using var frameBuffer = Bitmap.Lock();

        unsafe
        {
            var backBuffer = (uint*)frameBuffer.Address.ToPointer();
            var stride = frameBuffer.RowBytes;

            Parallel.For(yStart, yStart + height, (scanline) =>
            {
                var dest = backBuffer + scanline * stride / 4 + xStart;
                var src = Image.GetPixelRowSpan(scanline);

                for (int x = 0; x < width; x++)
                {
                    dest[x] = TranslateColor(x + xStart, src);
                }
            });
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint TranslateColor(int x, in Span<ColorRgba32> sourceRow)
    {
        var inputColor = sourceRow[x].Color;
        uint outputColor = (inputColor & 0xFF00FF00) | BitOperations.RotateLeft(inputColor & 0xFF00FF, 16);

        return outputColor;
    }
}
