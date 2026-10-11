using System;
using System.Linq;

namespace DemoArtGenerator.Scenes;

public static class Dungeon
{
    private const byte Outline = 0x1;
    private const byte White = 0x2;
    private const byte Yellow = 0x3;
    private const byte Orange = 0x4;
    private const byte Red = 0x5;
    private const byte WallDark = 0x6;
    private const byte Brick = 0x7;
    private const byte BrickLight = 0x8;
    private const byte Mortar = 0x9;
    private const byte Floor = 0xA;
    private const byte FloorLine = 0xB;
    private const byte Gold = 0xC;
    private const byte Wood = 0xD;
    private const byte WoodDark = 0xE;
    private const byte Glow = 0xF;
    private const int FloorTop = 70;

    public static Scene Scene { get; } = new("Dungeon",
    [
        (0, 0, 0), (1, 1, 2), (31, 31, 28), (31, 27, 8), (31, 15, 4), (24, 5, 4), (5, 4, 7), (10, 8, 12),
        (14, 12, 16), (3, 2, 5), (8, 6, 9), (12, 10, 13), (29, 23, 6), (17, 9, 4), (9, 5, 3), (21, 11, 8),
    ], Draw);

    private static readonly string[] _flame =
    [
        "...3...",
        "..33...",
        "..343..",
        ".3443..",
        ".34243.",
        "3442443",
        "3422243",
        ".34243.",
        "..353..",
    ];

    private static readonly (int X, int Y)[] _torches = [(40, 38), (216, 38)];

    private static void Draw(Canvas c)
    {
        for (int y = Canvas.PanelTop; y < FloorTop; y++)
        {
            var row = (y - Canvas.PanelTop) / 7;
            for (int x = 0; x < Canvas.Width; x++)
            {
                var bx = x + (row % 2) * 8;
                var mortar = (y - Canvas.PanelTop) % 7 == 6 || bx % 16 == 15;
                var light = _torches.Min(t => Math.Sqrt(Math.Pow(x - t.X, 2) + Math.Pow(y - t.Y - 4, 2)));
                var glowLevel = light < 9 ? 4 : light < 13 ? 3 : light < 17 ? 2 : light < 21 ? 1 : 0;
                c[x, y] = mortar ? Mortar
                    : Canvas.Dither(x, y, glowLevel) ? Glow
                    : (y - Canvas.PanelTop) % 7 == 0 ? BrickLight
                    : Canvas.Hash(bx / 16, row, 6) % 5 == 0 ? WallDark
                    : Brick;
            }
        }

        const int depth = Canvas.Height - FloorTop;
        c.Fill(0, FloorTop, Canvas.Width, depth, Floor);
        c.Fill(0, FloorTop, Canvas.Width, 1, Outline);
        for (int k = 1; k <= 4; k++)
            c.Fill(0, FloorTop + (int)(depth * (k / 4.0) * (k / 4.0)), Canvas.Width, 1, FloorLine);
        for (int i = -10; i <= 10; i++)
            for (int y = FloorTop + 1; y < Canvas.Height; y++)
                c[(int)Math.Round(128 + i * 26.0 * (y - FloorTop) / depth), y] = FloorLine;

        foreach (var (x, y) in _torches)
        {
            c.Fill(x - 2, y + 9, 5, 2, Outline);
            c.Fill(x - 1, y + 9, 3, 12, Outline);
            c.Fill(x, y + 9, 1, 11, Wood);
            c.Stamp(_flame, x - 3, y);
        }

        DrawChest(c, 112, 70);

        foreach (var (x, y) in new[] { (104, 66), (152, 62), (146, 82) })
        {
            c[x, y] = White;
            c[x - 1, y] = Yellow;
            c[x + 1, y] = Yellow;
            c[x, y - 1] = Yellow;
            c[x, y + 1] = Yellow;
        }

        c.ClipPanel();
        Title.Draw(c, row => row < 10 ? Yellow : row < 22 ? Orange : Red, Outline);
    }

    private static void DrawChest(Canvas c, int x, int y)
    {
        c.Fill(x, y + 2, 32, 18, Outline);
        c.Fill(x + 2, y, 28, 2, Outline);
        c.Fill(x + 1, y + 1, 30, 7, Wood);
        c.Fill(x + 2, y + 1, 28, 1, Orange);
        c.Fill(x + 1, y + 8, 30, 1, Outline);
        c.Fill(x + 1, y + 9, 30, 10, Wood);
        c.Fill(x + 1, y + 16, 30, 3, WoodDark);
        foreach (var bx in new[] { x + 4, x + 25 })
            c.Fill(bx, y + 1, 3, 18, Gold);
        c.Fill(x + 13, y + 6, 6, 7, Outline);
        c.Fill(x + 14, y + 7, 4, 5, Gold);
        c.Fill(x + 15, y + 9, 2, 2, Outline);
    }
}
