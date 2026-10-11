using System;

namespace DemoArtGenerator.Scenes;

public static class Forest
{
    private const byte Outline = 0x1;
    private const byte White = 0x2;
    private const byte Lime = 0x3;
    private const byte Green = 0x4;
    private const byte SkyHigh = 0x5;
    private const byte SkyLow = 0x6;
    private const byte CloudShade = 0x7;
    private const byte FarHills = 0x8;
    private const byte MidHills = 0x9;
    private const byte NearHills = 0xA;
    private const byte Shade = 0xB;
    private const byte Path = 0xC;
    private const byte PathEdge = 0xD;
    private const byte Flower = 0xE;
    private const byte Trunk = 0xF;

    public static Scene Scene { get; } = new("Forest",
    [
        (0, 0, 0), (2, 4, 3), (31, 31, 31), (24, 30, 10), (11, 25, 8), (12, 22, 31), (22, 28, 31), (24, 27, 31),
        (12, 20, 18), (8, 20, 10), (5, 16, 6), (2, 9, 4), (27, 22, 14), (20, 14, 8), (31, 9, 11), (14, 8, 4),
    ], Draw);

    private static double Far(int x) => 54 + 6 * Math.Sin(x / 19.0) + 3 * Math.Sin(x / 7.0);
    private static double Mid(int x) => 66 + 6 * Math.Sin(x / 27.0 + 2) + 2 * Math.Sin(x / 11.0);
    private static double Near(int x) => 80 + 4 * Math.Sin(x / 31.0 + 1);

    private static void Draw(Canvas c)
    {
        c.Bands(Canvas.PanelTop, Canvas.Height, (Canvas.PanelTop, SkyHigh), (48, SkyLow));
        DrawCloud(c, 20, 46);
        DrawCloud(c, 196, 44);

        c.Ridge(Far, FarHills);
        c.Ridge(Mid, MidHills);
        for (int x = 0; x < Canvas.Width; x++)
            c[x, (int)Math.Round(Mid(x))] = Green;

        foreach (var x in new[] { 12, 34, 58, 150, 174, 222, 244 })
            DrawTree(c, x, (int)Math.Round(Mid(x)) + 1, 5 + (int)(Canvas.Hash(x, 0, 9) % 3));

        c.Ridge(Near, NearHills);
        for (int x = 0; x < Canvas.Width; x++)
        {
            var top = (int)Math.Round(Near(x));
            c[x, top] = MidHills;
            if (Canvas.Hash(x, 1, 4) % 4 == 0)
                c[x, top - 1] = MidHills;
            for (int y = top + 1; y < Canvas.Height; y++)
            {
                var h = Canvas.Hash(x, y, 2) % 90;
                if (h == 0)
                    c[x, y] = Flower;
                else if (h == 1)
                    c[x, y] = White;
                else if (h < 6)
                    c[x, y] = MidHills;
            }
        }

        for (int y = (int)Near(128) - 1; y < Canvas.Height; y++)
        {
            var t = (y - Near(128)) / (Canvas.Height - Near(128));
            var center = 128 + 18 * Math.Sin(t * 3) * (1 - t) - 8 * t;
            var half = 1 + t * 9;
            for (int x = (int)(center - half); x <= (int)(center + half); x++)
                c[x, y] = x == (int)(center - half) || x == (int)(center + half) ? PathEdge : Path;
        }

        c.ClipPanel();
        Title.Draw(c, row => row < 10 ? White : row < 22 ? Lime : Green, Outline);
    }

    private static void DrawCloud(Canvas c, int x, int y)
    {
        c.Shape((px, py) => py <= y + 3 && (In(px, py, x, y, 5) || In(px, py, x + 7, y - 3, 6) || In(px, py, x + 15, y, 5)), White);
        c.Shape((px, py) => py == y + 3 && c[px, py] == White, CloudShade);
    }

    private static void DrawTree(Canvas c, int x, int ground, int radius)
    {
        c.Fill(x - 1, ground - radius - 2, 2, radius + 2, Trunk);
        var cy = ground - radius * 2;
        c.Disc(x, cy, radius + 1, Outline);
        c.Shape((px, py) => In(px, py, x, cy, radius), Green);
        c.Shape((px, py) => In(px, py, x, cy, radius) && py > cy + 1 && Canvas.Dither(px, py, py > cy + 3 ? 4 : 2), NearHills);
        c.Shape((px, py) => In(px, py, x, cy, radius) && py > cy + radius - 2, Shade);
        c[x - radius / 2, cy - radius / 2] = Lime;
        c[x - radius / 2 + 1, cy - radius / 2] = Lime;
        c[x - radius / 2, cy - radius / 2 + 1] = Lime;
    }

    private static bool In(int px, int py, double cx, double cy, double r) =>
        Math.Pow(px + 0.5 - cx, 2) + Math.Pow(py + 0.5 - cy, 2) <= r * r;
}
