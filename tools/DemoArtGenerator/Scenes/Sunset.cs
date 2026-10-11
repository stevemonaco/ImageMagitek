using System;

namespace DemoArtGenerator.Scenes;

public static class Sunset
{
    private const byte Outline = 0x1;
    private const byte White = 0x2;
    private const byte Yellow = 0x3;
    private const byte Orange = 0x4;
    private const byte SkyDeep = 0x5;
    private const byte SkyPurple = 0x6;
    private const byte SkyMagenta = 0x7;
    private const byte SkyPink = 0x8;
    private const byte Sun = 0x9;
    private const byte Floor = 0xA;
    private const byte Grid = 0xB;
    private const int Horizon = 64;

    public static Scene Scene { get; } = new("Sunset",
    [
        (0, 0, 0), (2, 1, 6), (31, 31, 28), (31, 27, 12), (31, 17, 6), (6, 3, 14), (14, 5, 20), (24, 7, 18),
        (31, 13, 13), (31, 25, 9), (3, 2, 9), (4, 24, 29), (27, 20, 12), (31, 14, 20), (8, 25, 12), (9, 15, 31),
    ], Draw);

    private static readonly string[] _pencil =
    [
        "...............111..",
        ".............1DDD1..",
        "............1DDD1...",
        "...........1BBB1....",
        "..........1BBB1.....",
        ".........12991......",
        "........12991.......",
        ".......12991........",
        "......12991.........",
        ".....12991..........",
        "....1CCC1...........",
        "...1CC1.............",
        "..1C1...............",
        ".11.................",
    ];

    private static readonly string[] _swatches =
    [
        "1111111111111111111111",
        "12DDDDD12EEEEE12FFFFF1",
        "1D1DD1D1E1EE1E1F1FF1F1",
        "1DDDDDD1EEEEEE1FFFFFF1",
        "1D1DD1D1E1EE1E1F1FF1F1",
        "1DD11DD1EE11EE1FF11FF1",
        "1DDDDDD1EEEEEE1FFFFFF1",
        "1111111111111111111111",
    ];

    private static void Draw(Canvas c)
    {
        c.Bands(Canvas.PanelTop, Horizon, (Canvas.PanelTop, SkyDeep), (28, SkyPurple), (40, SkyMagenta), (52, SkyPink));
        DrawSun(c);
        DrawFloor(c);
        c.ClipPanel();
        Title.Draw(c, row => row < 10 ? White : row < 22 ? Yellow : Orange, Outline);
        c.Stamp(_pencil, 16, 70);
        c.Stamp(_swatches, 218, 80);
    }

    private static void DrawSun(Canvas c)
    {
        const int centerX = Canvas.Width / 2;
        const int radius = 28;

        for (int y = Horizon - radius; y < Horizon; y++)
        {
            var stripeDepth = y - 42;
            if (stripeDepth > 0 && y % 5 < Math.Min(1 + stripeDepth / 7, 3))
                continue;

            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                var dx = x - centerX + 0.5;
                var dy = y - Horizon + 0.5;
                if (dx * dx + dy * dy <= radius * radius)
                    c[x, y] = y < 50 ? Sun : Orange;
            }
        }
    }

    private static void DrawFloor(Canvas c)
    {
        const int depth = Canvas.Height - Horizon;

        c.Fill(0, Horizon, Canvas.Width, depth, Floor);

        for (int k = 0; k <= 6; k++)
            c.Fill(0, Horizon + (int)(depth * (k / 6.0) * (k / 6.0)), Canvas.Width, 1, Grid);

        for (int i = -8; i <= 8; i++)
        {
            for (int y = Horizon; y < Canvas.Height; y++)
            {
                var x0 = (int)Math.Round(Canvas.Width / 2.0 + i * 32.0 * (y - Horizon) / (depth - 1));
                var x1 = (int)Math.Round(Canvas.Width / 2.0 + i * 32.0 * (y + 1 - Horizon) / (depth - 1));
                for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                    if (x == x0 || x != x1)
                        c[x, y] = Grid;
            }
        }
    }
}
