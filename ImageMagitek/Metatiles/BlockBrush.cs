using System;
using System.Drawing;

namespace ImageMagitek.Metatiles;

/// <summary>
/// A rectangle of metatile numbers to plot into a block grid. Null cells are transparent.
/// </summary>
public sealed class BlockBrush
{
    private readonly int?[,] _cells;

    public int Width => _cells.GetLength(1);
    public int Height => _cells.GetLength(0);
    public Size Size => new(Width, Height);

    /// <param name="cells">Cells indexed [y, x]</param>
    /// <exception cref="ArgumentException">The brush has no cells</exception>
    public BlockBrush(int?[,] cells)
    {
        if (cells.GetLength(0) == 0 || cells.GetLength(1) == 0)
            throw new ArgumentException("A block brush must have at least one cell", nameof(cells));

        _cells = cells;
    }

    public static BlockBrush Single(int number) => new(new int?[,] { { number } });

    /// <summary>
    /// A brush of the cells in <paramref name="clipped"/>, read by <paramref name="cellAt"/> at their absolute (x, y)
    /// </summary>
    /// <returns>Null when every cell is null</returns>
    internal static BlockBrush? FromCells(Rectangle clipped, Func<int, int, int?> cellAt)
    {
        if (clipped.Width <= 0 || clipped.Height <= 0)
            return null;

        var cells = new int?[clipped.Height, clipped.Width];
        var hasBlock = false;

        for (int y = 0; y < clipped.Height; y++)
        {
            for (int x = 0; x < clipped.Width; x++)
            {
                cells[y, x] = cellAt(x + clipped.X, y + clipped.Y);
                hasBlock |= cells[y, x] is not null;
            }
        }

        return hasBlock ? new BlockBrush(cells) : null;
    }

    public int? GetCell(int x, int y) => _cells[y, x];
}
