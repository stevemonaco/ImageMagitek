using System;
using System.Collections.Generic;
using System.Drawing;

namespace TileShop.Shared.Tools;

public static class PixelLine
{
    /// <summary>
    /// Rasterizes the 8-connected Bresenham line from <paramref name="start"/> to <paramref name="end"/>,
    /// one pixel per major-axis step with both endpoints included.
    /// </summary>
    public static IEnumerable<Point> Points(Point start, Point end)
    {
        int dx = Math.Abs(end.X - start.X);
        int dy = -Math.Abs(end.Y - start.Y);
        int stepX = start.X < end.X ? 1 : -1;
        int stepY = start.Y < end.Y ? 1 : -1;
        int error = dx + dy;

        int x = start.X;
        int y = start.Y;

        while (true)
        {
            yield return new Point(x, y);

            if (x == end.X && y == end.Y)
                yield break;

            int doubledError = 2 * error;
            if (doubledError >= dy)
            {
                error += dy;
                x += stepX;
            }

            if (doubledError <= dx)
            {
                error += dx;
                y += stepY;
            }
        }
    }
}
