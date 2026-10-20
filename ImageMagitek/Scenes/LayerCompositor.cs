using System;
using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Colors;

namespace ImageMagitek.Scenes;

/// <summary>
/// Stacks layer images back to front over a backdrop with straight-alpha source-over, clipped to the canvas.
/// </summary>
public static class LayerCompositor
{
    /// <summary>
    /// Draws each visible layer of <paramref name="layers"/>, in order from back to front, onto a canvas filled with
    /// <paramref name="backdrop"/>, or transparent without one. The layer images are not changed.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The canvas width or height is not positive</exception>
    public static LayerImage Composite(Size canvasSize, IEnumerable<CompositorLayer> layers, ColorRgba32? backdrop = null)
    {
        if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(canvasSize), canvasSize, "The canvas must have a positive width and height");

        var width = canvasSize.Width;
        var pixels = new ColorRgba32[width * canvasSize.Height];
        Array.Fill(pixels, backdrop ?? default);
        var canvas = new Rectangle(Point.Empty, canvasSize);

        foreach (var layer in layers)
        {
            if (!layer.IsVisible)
                continue;

            var image = layer.Image;
            var bounds = Rectangle.Intersect(canvas, new Rectangle(layer.Position, new Size(image.Width, image.Height)));
            if (bounds.Width <= 0 || bounds.Height <= 0)
                continue;

            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                var src = image.Pixels.AsSpan((y - layer.Position.Y) * image.Width + bounds.Left - layer.Position.X, bounds.Width);
                var dest = pixels.AsSpan(y * width + bounds.Left, bounds.Width);

                for (int x = 0; x < src.Length; x++)
                    dest[x] = Blend(src[x], dest[x]);
            }
        }

        return new LayerImage(width, canvasSize.Height, pixels);
    }

    private static ColorRgba32 Blend(ColorRgba32 source, ColorRgba32 dest)
    {
        if (source.A == 0)
            return dest;

        if (source.A == 255)
            return source;

        int sa = source.A;
        var weightedDest = dest.A * (255 - sa);
        var outA255 = sa * 255 + weightedDest;
        var outA = (byte)((outA255 + 127) / 255);
        return new ColorRgba32(Channel(source.R, dest.R), Channel(source.G, dest.G), Channel(source.B, dest.B), outA);

        byte Channel(byte sc, byte dc) => (byte)((sc * sa * 255 + dc * weightedDest + outA255 / 2) / outA255);
    }
}
