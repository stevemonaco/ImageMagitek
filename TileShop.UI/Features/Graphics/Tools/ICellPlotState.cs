using System;
using System.Drawing;

namespace TileShop.UI.Features.Graphics.Tools;

/// <summary>
/// An editor that plots whole cells (tiles or blocks) through <see cref="TileStrokeToolHandler{TState}"/> and <see cref="CellRectangleToolHandler{TState}"/>
/// </summary>
public interface ICellPlotState
{
    Point CellAt(int pixelX, int pixelY);

    /// <summary>
    /// The pixel bounds of <paramref name="cells"/> within the image, or null when none of them is in it
    /// </summary>
    Rectangle? CellsToPixels(Rectangle cells);

    bool IsCellStrokeActive { get; }
    void BeginCellStroke(string name);

    /// <summary>
    /// Ends the stroke, recording one history step when it changed a cell
    /// </summary>
    void EndCellStroke();

    void UpdateActivityMessage(int pixelX, int pixelY);
}

public static class CellRect
{
    /// <summary>
    /// The rectangle of cells spanning <paramref name="a"/> and <paramref name="b"/> inclusive
    /// </summary>
    public static Rectangle Between(Point a, Point b) =>
        Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X) + 1, Math.Max(a.Y, b.Y) + 1);

    /// <summary>
    /// The pixel bounds of <paramref name="cells"/> clipped to a grid of <paramref name="gridSize"/> cells, or null when none of them is in it
    /// </summary>
    public static Rectangle? ToPixels(Rectangle cells, Size cellSize, Size gridSize)
    {
        var clipped = Rectangle.Intersect(cells, new Rectangle(Point.Empty, gridSize));
        if (clipped.Width <= 0 || clipped.Height <= 0)
            return null;

        return new Rectangle(clipped.X * cellSize.Width, clipped.Y * cellSize.Height, clipped.Width * cellSize.Width, clipped.Height * cellSize.Height);
    }
}
