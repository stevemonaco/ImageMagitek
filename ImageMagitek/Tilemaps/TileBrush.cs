using System;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// One cell of a <see cref="TileBrush"/>: a tile index of the tile set and how the tile is mirrored
/// </summary>
public readonly record struct TileBrushCell(int TileIndex, MirrorOperation Mirror);

/// <summary>
/// A rectangle of tiles to plot into a tilemap, with one palette slot and whole-brush flips. Null cells are transparent.
/// </summary>
public sealed class TileBrush
{
    private readonly TileBrushCell?[,] _cells;

    public int Width => _cells.GetLength(1);
    public int Height => _cells.GetLength(0);
    public int Slot { get; }
    public bool HorizontalFlip { get; }
    public bool VerticalFlip { get; }

    private MirrorOperation FlipMirror => (HorizontalFlip, VerticalFlip) switch
    {
        (true, true) => MirrorOperation.Both,
        (true, false) => MirrorOperation.Horizontal,
        (false, true) => MirrorOperation.Vertical,
        _ => MirrorOperation.None
    };

    /// <param name="cells">Cells indexed [y, x]</param>
    /// <exception cref="ArgumentException">The brush has no cells</exception>
    public TileBrush(TileBrushCell?[,] cells, int slot = 0, bool horizontalFlip = false, bool verticalFlip = false)
    {
        if (cells.GetLength(0) == 0 || cells.GetLength(1) == 0)
            throw new ArgumentException("A tile brush must have at least one cell", nameof(cells));

        _cells = cells;
        Slot = slot;
        HorizontalFlip = horizontalFlip;
        VerticalFlip = verticalFlip;
    }

    public static TileBrush Single(int tileIndex, int slot = 0, bool horizontalFlip = false, bool verticalFlip = false) =>
        new(new TileBrushCell?[,] { { new TileBrushCell(tileIndex, MirrorOperation.None) } }, slot, horizontalFlip, verticalFlip);

    /// <summary>
    /// The cell as laid out before the brush flips are applied
    /// </summary>
    public TileBrushCell? GetCell(int x, int y) => _cells[y, x];

    /// <summary>
    /// The cell plotted at (<paramref name="x"/>, <paramref name="y"/>): the flips mirror the layout and each cell's tile
    /// </summary>
    public TileBrushCell? GetEffectiveCell(int x, int y)
    {
        var sourceX = HorizontalFlip ? Width - 1 - x : x;
        var sourceY = VerticalFlip ? Height - 1 - y : y;

        return _cells[sourceY, sourceX] is { } cell ? cell with { Mirror = cell.Mirror.Combine(FlipMirror) } : null;
    }

    public TileBrush WithFlips(bool horizontalFlip, bool verticalFlip) => new(_cells, Slot, horizontalFlip, verticalFlip);

    public TileBrush WithSlot(int slot) => new(_cells, slot, HorizontalFlip, VerticalFlip);
}
