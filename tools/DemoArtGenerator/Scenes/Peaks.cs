using System;
using System.Linq;

namespace DemoArtGenerator.Scenes;

public static class Peaks
{
    private const byte Outline = 0x1;
    private const byte White = 0x2;
    private const byte Ice = 0x3;
    private const byte Blue = 0x4;
    private const byte SkyHigh = 0x5;
    private const byte SkyMid = 0x6;
    private const byte SkyLow = 0x7;
    private const byte Aurora = 0x8;
    private const byte AuroraDim = 0x9;
    private const byte FarRange = 0xA;
    private const byte MidRange = 0xB;
    private const byte NearRange = 0xC;
    private const byte Snow = 0xD;
    private const byte Pine = 0xE;
    private const byte Moon = 0xF;

    public static Scene Scene { get; } = new("Peaks",
    [
        (0, 0, 0), (1, 1, 4), (31, 31, 31), (20, 28, 31), (8, 18, 30), (1, 2, 7), (3, 5, 13), (7, 9, 20),
        (8, 27, 17), (4, 15, 16), (9, 10, 21), (5, 6, 14), (3, 3, 9), (24, 27, 31), (2, 9, 7), (31, 30, 22),
    ], Draw);

    private static readonly (int X, int Y)[] _peaks = [(18, 52), (70, 44), (128, 54), (186, 42), (244, 50)];

    private static double Range(int x) =>
        _peaks.Min(p => p.Y + Math.Abs(x - p.X) * 0.85) + (Canvas.Hash(x, 0, 11) % 2);

    private static void Draw(Canvas c)
    {
        c.Bands(Canvas.PanelTop, Canvas.Height, (Canvas.PanelTop, SkyHigh), (38, SkyMid), (62, SkyLow));

        for (int i = 0; i < 70; i++)
        {
            int x = (int)(Canvas.Hash(i, 0, 3) % Canvas.Width), y = 15 + (int)(Canvas.Hash(i, 1, 3) % 45);
            c[x, y] = i % 4 == 0 ? Ice : White;
        }

        for (int x = 0; x < Canvas.Width; x++)
        {
            var top = 24 + 6 * Math.Sin(x / 23.0) + 3 * Math.Sin(x / 9.0);
            for (int y = (int)top; y < top + 12; y++)
            {
                var depth = y - top;
                if (depth < 3 || (depth < 7 && Canvas.Dither(x, y, 2)))
                    c[x, y] = Aurora;
                else if (Canvas.Dither(x, y, depth < 9 ? 2 : 1))
                    c[x, y] = AuroraDim;
            }
        }

        c.Disc(36, 40, 7, Moon);
        c.Disc(33, 38, 2, Ice);

        c.Ridge(x => 58 + 7 * Math.Sin(x / 17.0 + 1) + 4 * Math.Sin(x / 6.0), FarRange);
        c.Ridge(Range, MidRange);
        for (int x = 0; x < Canvas.Width; x++)
        {
            var top = (int)Math.Round(Range(x));
            var peak = _peaks.MinBy(p => p.Y + Math.Abs(x - p.X) * 0.85);
            var snowLine = peak.Y + 7 + (int)(Canvas.Hash(x, 1, 11) % 3);
            for (int y = top; y < snowLine; y++)
                c[x, y] = x < peak.X ? Snow : Ice;
        }

        c.Ridge(x => 82 + 3 * Math.Sin(x / 13.0), NearRange);
        for (int x = 6; x < Canvas.Width; x += 9 + (int)(Canvas.Hash(x, 2, 5) % 6))
            DrawPine(c, x, (int)Math.Round(84 + 3 * Math.Sin(x / 13.0)), 9 + (int)(Canvas.Hash(x, 3, 5) % 7));

        c.Ridge(x => 89 + 2 * Math.Sin(x / 19.0 + 2), Snow);
        for (int y = 91; y < Canvas.Height; y++)
            for (int x = 0; x < Canvas.Width; x++)
                if (Canvas.Dither(x, y, y - 90))
                    c[x, y] = Ice;

        c.ClipPanel();
        Title.Draw(c, row => row < 10 ? White : row < 22 ? Ice : Blue, Outline);
    }

    private static void DrawPine(Canvas c, int x, int ground, int height)
    {
        for (int row = 0; row < height; row++)
        {
            var half = (row % 4) + row / 3;
            for (int dx = -half; dx <= half; dx++)
                c[x + dx, ground - height + row] = Pine;
        }
        c[x, ground] = Pine;
    }
}
