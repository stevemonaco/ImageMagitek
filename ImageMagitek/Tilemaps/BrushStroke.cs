using System;
using System.Drawing;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// Cell arithmetic shared by the tile and block plotters
/// </summary>
public static class BrushStroke
{
    /// <summary>
    /// Snaps <paramref name="cell"/> to the grid of brush-sized steps from <paramref name="origin"/>, so successive stamps tile instead of overlapping
    /// </summary>
    public static Point SnapToStroke(Size brushSize, Point origin, Point cell) =>
        new(origin.X + FloorDiv(cell.X - origin.X, brushSize.Width) * brushSize.Width,
            origin.Y + FloorDiv(cell.Y - origin.Y, brushSize.Height) * brushSize.Height);

    /// <summary>
    /// The non-negative remainder, so a brush repeats the same way left and right of its origin
    /// </summary>
    public static int Mod(int value, int divisor) => (value % divisor + divisor) % divisor;

    private static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);
}
