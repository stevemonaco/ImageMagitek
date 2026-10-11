using System;

namespace DemoArtGenerator.Scenes;

public static class Space
{
    private const byte Outline = 0x1;
    private const byte White = 0x2;
    private const byte Cyan = 0x3;
    private const byte Purple = 0x4;
    private const byte SpaceHigh = 0x5;
    private const byte SpaceLow = 0x6;
    private const byte Nebula = 0x7;
    private const byte NebulaBright = 0x8;
    private const byte PlanetLight = 0x9;
    private const byte Planet = 0xA;
    private const byte PlanetDark = 0xB;
    private const byte Ring = 0xC;
    private const byte RingDark = 0xD;
    private const byte Moon = 0xE;
    private const byte MoonDark = 0xF;

    public static Scene Scene { get; } = new("Space",
    [
        (0, 0, 0), (1, 0, 3), (31, 31, 31), (12, 26, 31), (17, 9, 28), (1, 0, 4), (3, 1, 9), (9, 3, 14),
        (20, 6, 18), (31, 23, 13), (26, 14, 8), (13, 6, 7), (25, 24, 28), (14, 13, 20), (19, 19, 23), (10, 10, 14),
    ], Draw);

    private const double PlanetX = 196, PlanetY = 76, PlanetR = 24;

    private static void Draw(Canvas c)
    {
        c.Bands(Canvas.PanelTop, Canvas.Height, (Canvas.PanelTop, SpaceHigh), (52, SpaceLow));

        for (int y = Canvas.PanelTop; y < Canvas.Height; y++)
        {
            for (int x = 0; x < Canvas.Width; x++)
            {
                var field = Math.Sin(x / 21.0) + Math.Sin(y / 9.0 + x / 40.0) + Math.Sin((x + y) / 17.0) + Math.Sin((x - 2 * y) / 31.0);
                if (field > 2.3)
                    c[x, y] = Canvas.Dither(x, y, (int)((field - 2.3) * 8) + 1) ? NebulaBright : Nebula;
                else if (field > 1.4 && Canvas.Dither(x, y, (int)((field - 1.4) * 4) + 1))
                    c[x, y] = Nebula;
            }
        }

        for (int i = 0; i < 90; i++)
        {
            int x = (int)(Canvas.Hash(i, 0, 1) % Canvas.Width), y = Canvas.PanelTop + (int)(Canvas.Hash(i, 1, 1) % 82);
            c[x, y] = i % 5 == 0 ? Cyan : White;
            if (i % 17 == 0)
            {
                c[x - 1, y] = Cyan;
                c[x + 1, y] = Cyan;
                c[x, y - 1] = Cyan;
                c[x, y + 1] = Cyan;
            }
        }

        DrawRing(c, front: false);
        c.Shape((x, y) => Distance(x, y, PlanetX, PlanetY) <= PlanetR, Planet);
        c.Shape((x, y) =>
        {
            var d = Distance(x, y, PlanetX, PlanetY);
            if (d > PlanetR)
                return false;
            var band = (int)(y + 3 * Math.Sin(x / 6.0)) % 7;
            return band < 2;
        }, PlanetLight);
        c.Shape((x, y) =>
        {
            if (Distance(x, y, PlanetX, PlanetY) > PlanetR)
                return false;
            var shade = (x - PlanetX + (y - PlanetY) * 0.6) / PlanetR;
            return shade > 0.55 || (shade > 0.3 && Canvas.Dither(x, y, 2));
        }, PlanetDark);
        DrawRing(c, front: true);

        c.Disc(58, 62, 10, Moon);
        c.Shape((x, y) => Distance(x, y, 58, 62) <= 10 && (x - 58 + (y - 62) * 0.5 > 4 || Distance(x, y, 54, 58) <= 2.5 || Distance(x, y, 61, 66) <= 2), MoonDark);

        c.ClipPanel();
        Title.Draw(c, row => row < 10 ? White : row < 22 ? Cyan : Purple, Outline);
    }

    private static void DrawRing(Canvas c, bool front) =>
        c.Shape((x, y) =>
        {
            var dx = (x + 0.5 - PlanetX) / 46.0;
            var dy = (y + 0.5 - PlanetY + (x - PlanetX) * 0.18) / 9.0;
            var e = dx * dx + dy * dy;
            return e >= 0.62 && e <= 1 && (front ? dy >= 0 : dy < 0);
        }, front ? Ring : RingDark);

    private static double Distance(int x, int y, double cx, double cy) => Math.Sqrt(Math.Pow(x + 0.5 - cx, 2) + Math.Pow(y + 0.5 - cy, 2));
}
