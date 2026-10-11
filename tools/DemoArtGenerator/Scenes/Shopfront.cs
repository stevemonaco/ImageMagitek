using System;

namespace DemoArtGenerator.Scenes;

/// <summary>
/// The app icon's shop, drawn full size on a street at dusk.
/// </summary>
public static class Shopfront
{
    private const byte Outline = 0x1;
    private const byte White = 0x2;
    private const byte Blue = 0x3;
    private const byte BlueDark = 0x4;
    private const byte BlueLight = 0x5;
    private const byte Orange = 0x6;
    private const byte OrangeDark = 0x7;
    private const byte Yellow = 0x8;
    private const byte SkyDeep = 0x9;
    private const byte SkyMid = 0xA;
    private const byte SkyLow = 0xB;
    private const byte Silhouette = 0xC;
    private const byte Road = 0xD;
    private const byte Pavement = 0xE;
    private const byte Door = 0xF;
    private const int Street = 86;

    public static Scene Scene { get; } = new("Shopfront",
    [
        (0, 0, 0), (2, 2, 5), (31, 31, 30), (0, 19, 30), (0, 13, 23), (17, 27, 31), (31, 19, 6), (25, 11, 3),
        (31, 28, 9), (3, 3, 10), (8, 5, 16), (17, 8, 20), (6, 4, 12), (7, 7, 11), (13, 13, 18), (9, 9, 14),
    ], Draw);

    private static readonly (int X, int Width, int Top)[] _buildings =
        [(6, 30, 52), (36, 28, 62), (64, 30, 50), (162, 30, 56), (192, 34, 48), (226, 26, 60)];

    private static void Draw(Canvas c)
    {
        c.Bands(Canvas.PanelTop, Street, (Canvas.PanelTop, SkyDeep), (36, SkyMid), (56, SkyLow));

        for (int i = 0; i < 40; i++)
            c[(int)(Canvas.Hash(i, 0, 7) % Canvas.Width), 16 + (int)(Canvas.Hash(i, 1, 7) % 30)] = White;

        c.Shape((x, y) => InDisc(x, y, 232, 30, 7) && !InDisc(x, y, 235, 28, 6), Yellow);

        foreach (var (left, width, top) in _buildings)
        {
            c.Fill(left, top, width, Street - top, Silhouette);
            for (int y = top + 4; y + 4 < Street - 2; y += 7)
                for (int x = left + 4; x + 3 < left + width - 2; x += 6)
                    c.Fill(x, y, 3, 4, Canvas.Hash(x, y, 3) % 3 == 0 ? SkyMid : Yellow);
        }

        foreach (var lampX in new[] { 96, 158 })
        {
            c.Fill(lampX, 62, 2, Street - 62, Outline);
            c.Fill(lampX - 2, 57, 6, 5, Outline);
            c.Fill(lampX - 1, 58, 4, 3, Yellow);
        }

        c.Fill(0, Street, Canvas.Width, 4, Pavement);
        c.Fill(0, Street + 3, Canvas.Width, 1, Outline);
        c.Fill(0, Street + 4, Canvas.Width, Canvas.Height - Street - 4, Road);
        for (int x = 4; x < Canvas.Width; x += 24)
            c.Fill(x, 93, 12, 1, Yellow);

        DrawShop(c);
        c.ClipPanel();
        Title.Draw(c, row => row < 10 ? White : row < 22 ? Yellow : Orange, Outline);
    }

    private static void DrawShop(Canvas c)
    {
        c.Fill(107, 56, 42, Street - 56 + 1, Outline);
        c.Fill(108, 56, 40, Street - 56, Orange);
        c.Fill(146, 56, 2, Street - 56, OrangeDark);
        c.Fill(108, 56, 40, 2, OrangeDark);

        c.Fill(113, 61, 20, 20, Outline);
        for (int ty = 0; ty < 3; ty++)
        {
            for (int tx = 0; tx < 3; tx++)
            {
                var light = (tx + ty) % 2 == 0;
                int x = 114 + tx * 6, y = 62 + ty * 6;
                c.Fill(x, y, 6, 6, light ? BlueLight : Blue);
                c.Fill(x + 5, y, 1, 6, light ? Blue : BlueDark);
                c.Fill(x, y + 5, 6, 1, light ? Blue : BlueDark);
            }
        }
        c[115, 63] = White;
        c[116, 63] = White;
        c[115, 64] = White;

        c.Fill(136, 64, 9, Street - 64, Outline);
        c.Fill(137, 65, 7, Street - 65, Door);
        c.Fill(138, 67, 5, 5, Outline);
        c.Fill(139, 68, 3, 3, BlueLight);
        c.Fill(142, 75, 1, 2, Yellow);

        DrawAwning(c);
    }

    private static void DrawAwning(Canvas c)
    {
        const int left = 104, top = 45, stripe = 6, stripes = 8;
        var mask = new bool[Canvas.Width * Canvas.Height];

        for (int s = 0; s < stripes; s++)
        {
            var x0 = left + s * stripe;
            var blue = s % 2 == 0;
            for (int x = x0; x < x0 + stripe; x++)
            {
                var hang = x - x0 is 0 or stripe - 1 ? 0 : x - x0 is 1 or stripe - 2 ? 1 : 2;
                for (int y = top; y <= top + 8 + hang; y++)
                {
                    c[x, y] = y >= top + 7 ? (blue ? BlueDark : BlueLight) : blue ? Blue : White;
                    mask[y * Canvas.Width + x] = true;
                }
            }
        }

        c.Outline(mask, Outline);
    }

    private static bool InDisc(int x, int y, double cx, double cy, double r) =>
        Math.Pow(x + 0.5 - cx, 2) + Math.Pow(y + 0.5 - cy, 2) <= r * r;
}
