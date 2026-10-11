using System;

namespace DemoArtGenerator.Scenes;

public static class Seaside
{
    private const byte Outline = 0x1;
    private const byte White = 0x2;
    private const byte Yellow = 0x3;
    private const byte Orange = 0x4;
    private const byte SkyHigh = 0x5;
    private const byte SkyLow = 0x6;
    private const byte CloudShade = 0x7;
    private const byte Sea = 0x8;
    private const byte SeaLight = 0x9;
    private const byte SeaDeep = 0xA;
    private const byte Sand = 0xB;
    private const byte SandShade = 0xC;
    private const byte Leaf = 0xD;
    private const byte LeafDark = 0xE;
    private const byte Trunk = 0xF;
    private const int Horizon = 62;

    public static Scene Scene { get; } = new("Seaside",
    [
        (0, 0, 0), (2, 3, 8), (31, 31, 31), (31, 28, 9), (31, 17, 6), (9, 19, 31), (18, 26, 31), (22, 25, 31),
        (2, 12, 24), (7, 20, 29), (1, 7, 17), (29, 25, 15), (23, 17, 9), (6, 22, 8), (2, 13, 6), (16, 10, 5),
    ], Draw);

    private static void Draw(Canvas c)
    {
        c.Bands(Canvas.PanelTop, Horizon, (Canvas.PanelTop, SkyHigh), (46, SkyLow));
        c.Disc(200, 44, 11, Orange);
        c.Disc(200, 44, 9, Yellow);
        DrawCloud(c, 26, 50);
        DrawCloud(c, 118, 54);

        c.Fill(0, Horizon, Canvas.Width, 3, SeaDeep);
        c.Fill(0, Horizon + 3, Canvas.Width, Canvas.Height - Horizon - 3, Sea);
        for (int y = Horizon + 4; y < Canvas.Height; y += 3)
            for (int x = 0; x < Canvas.Width; x++)
                if ((x + y * 7 + (int)(4 * Math.Sin(x / 11.0 + y))) % 16 < 3)
                    c[x, y] = SeaLight;
        for (int y = Horizon + 1; y < 80; y++)
            for (int x = 186; x <= 214; x++)
                if (Math.Abs(x - 200) < 12 - (y - Horizon) / 3 && Canvas.Hash(x, y, 5) % 3 == 0)
                    c[x, y] = Yellow;

        DrawIsland(c);
        DrawBoat(c, 150, 58);

        c.Ridge(x => 83 + 3 * Math.Sin(x / 21.0) + 2 * Math.Sin(x / 7.0 + 1), Sand);
        for (int x = 0; x < Canvas.Width; x++)
        {
            var shore = (int)Math.Round(83 + 3 * Math.Sin(x / 21.0) + 2 * Math.Sin(x / 7.0 + 1));
            c[x, shore - 1] = White;
            for (int y = shore + 6; y < Canvas.Height; y++)
                if (Canvas.Dither(x, y, Math.Min(4, (y - shore - 5) / 2)))
                    c[x, y] = SandShade;
        }

        c.ClipPanel();
        Title.Draw(c, row => row < 10 ? White : row < 22 ? Yellow : Orange, Outline);
    }

    private static void DrawCloud(Canvas c, int x, int y)
    {
        c.Shape((px, py) => py <= y + 3 && (In(px, py, x, y, 6) || In(px, py, x + 8, y - 4, 7) || In(px, py, x + 17, y - 1, 6) || In(px, py, x + 24, y + 1, 4)), White);
        c.Shape((px, py) => py >= y + 2 && py <= y + 3 && c[px, py] == White, CloudShade);
    }

    private static void DrawIsland(Canvas c)
    {
        c.Shape((x, y) => y >= Horizon + 4 && y <= Horizon + 9 && Math.Pow((x - 84) / 26.0, 2) + Math.Pow((y - Horizon - 9) / 6.0, 2) <= 1, Sand);
        c.Shape((x, y) => y >= Horizon + 8 && y <= Horizon + 9 && c[x, y] == Sand, SandShade);

        for (int t = 0; t < 20; t++)
        {
            var x = 84 + (int)(6 * Math.Pow(t / 20.0, 2));
            c[x, Horizon + 5 - t] = Trunk;
            c[x + 1, Horizon + 5 - t] = t % 3 == 0 ? SandShade : Trunk;
        }

        const int crownX = 90, crownY = Horizon - 15;
        foreach (var (dir, lift, reach) in new[] { (-1, 0, 13), (-1, 3, 9), (1, 0, 13), (1, 3, 9) })
        {
            for (int i = 0; i <= reach; i++)
            {
                var x = crownX + dir * i;
                var y = crownY - lift + i * i * (lift + 3) / (reach * reach) * 2 - (i < reach / 2 ? lift * i / reach : 0);
                c[x, y] = Leaf;
                c[x, y + 1] = LeafDark;
                if (i > 1 && i < reach - 2)
                    c[x, y - 1] = Leaf;
            }
        }
        c[89, crownY + 2] = Trunk;
        c[92, crownY + 2] = Trunk;
    }

    private static void DrawBoat(Canvas c, int x, int y)
    {
        var mask = new bool[Canvas.Width * Canvas.Height];
        void Paint(int px, int py, byte color)
        {
            c[px, py] = color;
            mask[py * Canvas.Width + px] = true;
        }

        for (int row = 0; row < 10; row++)
            for (int col = 0; col <= row; col++)
                Paint(x + 9 - col, y + row, White);
        for (int row = 2; row < 10; row++)
            for (int col = 0; col <= (row - 2) / 2; col++)
                Paint(x + 11 + col, y + row, Orange);
        for (int row = 0; row < 11; row++)
            Paint(x + 10, y + row, Trunk);
        for (int row = 0; row < 3; row++)
            for (int col = row; col < 20 - row; col++)
                Paint(x + col, y + 11 + row, row == 0 ? Orange : Trunk);

        c.Outline(mask, Outline);
    }

    private static bool In(int px, int py, double cx, double cy, double r) =>
        Math.Pow(px + 0.5 - cx, 2) + Math.Pow(py + 0.5 - cy, 2) <= r * r;
}
