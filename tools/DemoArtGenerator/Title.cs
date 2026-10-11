using System;
using System.Collections.Generic;
using System.Linq;

namespace DemoArtGenerator;

/// <summary>
/// The chunky "TileShop" wordmark every scene draws across its top edge.
/// </summary>
public static class Title
{
    private const string Text = "TileShop";
    private const int Top = 4;
    private const int Scale = 4;

    private static readonly Dictionary<char, string[]> _glyphs = new()
    {
        ['T'] = ["######", "######", "..##..", "..##..", "..##..", "..##..", "..##..", "......", "......"],
        ['i'] = ["##", "..", "##", "##", "##", "##", "##", "..", ".."],
        ['l'] = ["##", "##", "##", "##", "##", "##", "##", "..", ".."],
        ['e'] = [".....", ".....", ".###.", "##.##", "#####", "##...", ".####", ".....", "....."],
        ['S'] = [".####.", "##..##", "##....", ".####.", "....##", "##..##", ".####.", "......", "......"],
        ['h'] = ["##...", "##...", "####.", "##.##", "##.##", "##.##", "##.##", ".....", "....."],
        ['o'] = [".....", ".....", ".###.", "##.##", "##.##", "##.##", ".###.", ".....", "....."],
        ['p'] = [".....", ".....", "####.", "##.##", "##.##", "####.", "##...", "##...", "##..."],
    };

    /// <summary>
    /// Draws the wordmark with a 2 px outline that extends 3 px further down as a drop shadow.
    /// </summary>
    /// <param name="fill">The letter color for a row, counted in pixels from the top of the letters</param>
    public static void Draw(Canvas canvas, Func<int, byte> fill, byte outline)
    {
        var mask = new bool[Canvas.Width * Canvas.Height];
        var width = Text.Sum(c => _glyphs[c][0].Length + 1) - 1;
        var left = (Canvas.Width - width * Scale) / 2;

        foreach (var c in Text)
        {
            var glyph = _glyphs[c];
            for (int gy = 0; gy < glyph.Length; gy++)
                for (int gx = 0; gx < glyph[gy].Length; gx++)
                    if (glyph[gy][gx] == '#')
                        for (int y = 0; y < Scale; y++)
                            for (int x = 0; x < Scale; x++)
                                mask[(Top + gy * Scale + y) * Canvas.Width + left + gx * Scale + x] = true;

            left += (glyph[0].Length + 1) * Scale;
        }

        for (int y = 0; y < Canvas.Height; y++)
            for (int x = 0; x < Canvas.Width; x++)
                if (mask[y * Canvas.Width + x])
                    canvas[x, y] = fill(y - Top);
                else if (IsEdge(mask, x, y))
                    canvas[x, y] = outline;
    }

    private static bool IsEdge(bool[] mask, int x, int y)
    {
        for (int dy = -5; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                if (Canvas.Contains(x + dx, y + dy) && mask[(y + dy) * Canvas.Width + x + dx])
                    return true;

        return false;
    }
}
