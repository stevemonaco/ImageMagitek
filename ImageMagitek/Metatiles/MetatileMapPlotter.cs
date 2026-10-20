using System.Drawing;
using ImageMagitek.Tilemaps;

namespace ImageMagitek.Metatiles;

/// <summary>
/// Plots whole blocks into a <see cref="BlockGrid"/>, which <see cref="MetatileMap.WriteBlocks"/> later writes back
/// </summary>
public sealed class MetatileMapPlotter
{
    private readonly MetatileMap _map;
    private readonly BlockGrid _grid;

    public MetatileMapPlotter(MetatileMap map, BlockGrid grid)
    {
        _map = map;
        _grid = grid;
    }

    /// <summary>
    /// Why <paramref name="brush"/> cannot be plotted into this map, or null when it can
    /// </summary>
    public string? FindBrushError(BlockBrush brush)
    {
        for (int y = 0; y < brush.Height; y++)
        {
            for (int x = 0; x < brush.Width; x++)
            {
                if (brush.GetCell(x, y) is { } number && _map.FindSetFor(number) is null)
                    return $"Block {number} is in no metatile set of map '{_map.Name}'";
            }
        }

        return null;
    }

    /// <summary>
    /// Plots <paramref name="brush"/> with its top-left at the block, clipped to the map
    /// </summary>
    /// <returns>True when a block changed; a brush refused by <see cref="FindBrushError"/> changes nothing</returns>
    public bool Stamp(BlockBrush brush, int blockX, int blockY)
    {
        if (FindBrushError(brush) is not null)
            return false;

        var changed = false;
        for (int y = 0; y < brush.Height; y++)
        {
            for (int x = 0; x < brush.Width; x++)
            {
                if (brush.GetCell(x, y) is { } number)
                    changed |= SetBlock(blockX + x, blockY + y, number);
            }
        }

        return changed;
    }

    /// <summary>
    /// Fills <paramref name="blocks"/> with <paramref name="brush"/> repeated from the rectangle's top-left, clipped to the map
    /// </summary>
    public bool Fill(BlockBrush brush, Rectangle blocks)
    {
        if (FindBrushError(brush) is not null)
            return false;

        var clipped = ClipToMap(blocks);
        var changed = false;

        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                if (brush.GetCell(BrushStroke.Mod(x - blocks.Left, brush.Width), BrushStroke.Mod(y - blocks.Top, brush.Height)) is { } number)
                    changed |= SetBlock(x, y, number);
            }
        }

        return changed;
    }

    /// <summary>
    /// Sets the blocks to the map's empty block
    /// </summary>
    public bool Erase(Rectangle blocks)
    {
        var clipped = ClipToMap(blocks);
        var changed = false;

        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            for (int x = clipped.Left; x < clipped.Right; x++)
                changed |= _grid.TrySet(x, y, _map.EmptyBlock);
        }

        return changed;
    }

    /// <summary>
    /// A brush of the blocks under <paramref name="blocks"/>; blocks with no number or a number outside every set are transparent
    /// </summary>
    /// <returns>Null when no block in the rectangle can be picked</returns>
    public BlockBrush? Pick(Rectangle blocks) =>
        BlockBrush.FromCells(ClipToMap(blocks), (x, y) => _grid[x, y] is { } n && _map.FindSetFor(n) is not null ? n : null);

    private bool SetBlock(int x, int y, int number) =>
        x >= 0 && y >= 0 && x < _grid.Width && y < _grid.Height && _grid.TrySet(x, y, number);

    private Rectangle ClipToMap(Rectangle blocks) => Rectangle.Intersect(blocks, new Rectangle(0, 0, _grid.Width, _grid.Height));
}
