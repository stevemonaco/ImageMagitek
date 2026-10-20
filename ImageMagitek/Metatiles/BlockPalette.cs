using System;
using System.Drawing;
using System.Linq;
using ImageMagitek.Scenes;

namespace ImageMagitek.Metatiles;

/// <summary>
/// The blocks a metatile map's brush is picked from, generated from its metatile sets
/// </summary>
public sealed class BlockPalette
{
    private const int Columns = 8;

    private readonly MetatileMap _map;
    private readonly int?[,] _numbers;

    public int Width => _numbers.GetLength(1);
    public int Height => _numbers.GetLength(0);

    private BlockPalette(MetatileMap map, int?[,] numbers)
    {
        _map = map;
        _numbers = numbers;
    }

    /// <summary>
    /// Lays out each set's metatile numbers in list order, eight to a row, each set starting on a new row
    /// </summary>
    public static BlockPalette Generate(MetatileMap map)
    {
        var rows = Math.Max(1, map.Sets.Sum(x => (x.Set.Count + Columns - 1) / Columns));
        var numbers = new int?[rows, Columns];
        var row = 0;

        foreach (var set in map.Sets)
        {
            for (int i = 0; i < set.Set.Count; i++)
                numbers[row + i / Columns, i % Columns] = set.Base + i;

            row += (set.Set.Count + Columns - 1) / Columns;
        }

        return new BlockPalette(map, numbers);
    }

    /// <summary>
    /// The block number of the cell, or null when the cell is empty or outside the palette
    /// </summary>
    public int? GetBlockNumber(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? _numbers[y, x] : null;

    public Point? FindCell(int number)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_numbers[y, x] == number)
                    return new Point(x, y);
            }
        }

        return null;
    }

    /// <summary>
    /// A brush of the blocks in <paramref name="cells"/>, clipped to the palette; empty cells become transparent
    /// </summary>
    /// <returns>Null when no cell in the rectangle holds a block</returns>
    public BlockBrush? CreateBrush(Rectangle cells) =>
        BlockBrush.FromCells(Rectangle.Intersect(cells, new Rectangle(0, 0, Width, Height)), (x, y) => _numbers[y, x]);

    /// <summary>
    /// Draws every block composited with the map's visible layers, as on the map
    /// </summary>
    public LayerImage Render() => _map.RenderBlocks(_numbers);
}
