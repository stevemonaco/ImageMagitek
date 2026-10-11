using System;
using System.Collections.Generic;
using System.Linq;

namespace DemoArtGenerator;

/// <summary>
/// A 256x96 grid of palette indices with the drawing operations the scenes share.
/// </summary>
public sealed class Canvas
{
    public const int Width = 256;
    public const int Height = 96;
    public const int PanelTop = 14;

    private static readonly int[] _bayer2x2 = [0, 2, 3, 1];

    public byte[] Pixels { get; } = new byte[Width * Height];

    public byte this[int x, int y]
    {
        get => Contains(x, y) ? Pixels[y * Width + x] : (byte)0;
        set
        {
            if (Contains(x, y))
                Pixels[y * Width + x] = value;
        }
    }

    public static bool Contains(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    /// A deterministic pseudo-random value for placing stars, windows and other scatter.
    /// </summary>
    public static uint Hash(int x, int y, int seed)
    {
        var h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }

    public static bool Dither(int x, int y, int level) => _bayer2x2[(y & 1) * 2 + (x & 1)] < level;

    public void Fill(int x, int y, int width, int height, byte color)
    {
        for (int j = y; j < y + height; j++)
            for (int i = x; i < x + width; i++)
                this[i, j] = color;
    }

    public void Disc(double cx, double cy, double radius, byte color) =>
        Shape((x, y) => Square(x + 0.5 - cx) + Square(y + 0.5 - cy) <= radius * radius, color);

    public void Shape(Func<int, int, bool> contains, byte color)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (contains(x, y))
                    Pixels[y * Width + x] = color;
    }

    /// <summary>
    /// Fills each column from <paramref name="top"/>(x) down to <paramref name="bottom"/>.
    /// </summary>
    public void Ridge(Func<int, double> top, byte color, int bottom = Height)
    {
        for (int x = 0; x < Width; x++)
            for (int y = Math.Max(0, (int)Math.Round(top(x))); y < bottom; y++)
                this[x, y] = color;
    }

    /// <summary>
    /// Horizontal color bands, each blending into the next over its last three rows with a 2x2 ordered dither.
    /// </summary>
    public void Bands(int top, int bottom, params (int StartY, byte Color)[] bands)
    {
        for (int y = top; y < bottom; y++)
        {
            var band = Array.FindLastIndex(bands, b => y >= b.StartY);
            var transition = band + 1 < bands.Length ? y - (bands[band + 1].StartY - 3) : -1;

            for (int x = 0; x < Width; x++)
                this[x, y] = transition >= 0 && Dither(x, y, transition + 1) ? bands[band + 1].Color : bands[band].Color;
        }
    }

    /// <summary>
    /// Draws a sprite of hex palette indices; '.' is left untouched.
    /// </summary>
    public void Stamp(IReadOnlyList<string> sprite, int left, int top)
    {
        for (int y = 0; y < sprite.Count; y++)
            for (int x = 0; x < sprite[y].Length; x++)
                if (sprite[y][x] != '.')
                    this[left + x, top + y] = Convert.ToByte(sprite[y][x].ToString(), 16);
    }

    /// <summary>
    /// Colors every pixel outside the mask that touches it horizontally or vertically.
    /// </summary>
    public void Outline(bool[] mask, byte color)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (!mask[y * Width + x] && new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) }.Any(p => Contains(p.Item1, p.Item2) && mask[p.Item2 * Width + p.Item1]))
                    Pixels[y * Width + x] = color;
    }

    /// <summary>
    /// Clears everything above <see cref="PanelTop"/> and outside the panel's rounded corners.
    /// </summary>
    public void ClipPanel(int cornerRadius = 6)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (y < PanelTop)
                {
                    Pixels[y * Width + x] = 0;
                    continue;
                }

                var cx = x < cornerRadius ? cornerRadius - x - 0.5 : x >= Width - cornerRadius ? x - (Width - cornerRadius) + 0.5 : 0;
                var cy = y < PanelTop + cornerRadius ? PanelTop + cornerRadius - y - 0.5 : y >= Height - cornerRadius ? y - (Height - cornerRadius) + 0.5 : 0;
                if (cx > 0 && cy > 0 && cx * cx + cy * cy > cornerRadius * cornerRadius)
                    Pixels[y * Width + x] = 0;
            }
        }
    }

    private static double Square(double v) => v * v;
}
