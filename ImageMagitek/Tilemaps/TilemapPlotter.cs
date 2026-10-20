using System.Drawing;
using ImageMagitek.Codec;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// Plots whole tiles into the edited copy of a tilemap, which <see cref="TilemapArranger.WriteEntries"/> later writes back
/// </summary>
public sealed class TilemapPlotter
{
    private readonly TilemapArranger _tilemap;
    private readonly ScatteredArranger _working;
    private readonly IGraphicsCodec?[] _slotCodecs;

    public TilemapPlotter(TilemapArranger tilemap, ScatteredArranger working)
    {
        _tilemap = tilemap;
        _working = working;
        _slotCodecs = new IGraphicsCodec?[tilemap.PaletteSlots.Count];
    }

    private int MapWidth => _working.ArrangerElementSize.Width;
    private int MapHeight => _working.ArrangerElementSize.Height;

    /// <summary>
    /// Why <paramref name="brush"/> cannot be plotted into this tilemap, or null when it can
    /// </summary>
    public string? FindBrushError(TileBrush brush)
    {
        var format = _tilemap.MapFormat;

        if (brush.Slot < 0 || brush.Slot >= _tilemap.PaletteSlots.Count)
            return $"Map format '{format.Name}' has no palette slot {brush.Slot}";

        if (brush.HorizontalFlip && format.HorizontalFlip is null)
            return $"Map format '{format.Name}' has no horizontal flip field";

        if (brush.VerticalFlip && format.VerticalFlip is null)
            return $"Map format '{format.Name}' has no vertical flip field";

        var first = _tilemap.FirstTileIndex;
        var last = first + _tilemap.TileCount - 1;

        for (int y = 0; y < brush.Height; y++)
        {
            for (int x = 0; x < brush.Width; x++)
            {
                if (brush.GetEffectiveCell(x, y) is not { } cell)
                    continue;

                if (cell.TileIndex < first || cell.TileIndex > last)
                    return _tilemap.TileCount == 0 ? "The tile set has no tiles" : $"Tile {cell.TileIndex} is not in the tile set (tiles {first} to {last})";

                if (cell.Mirror.FlipsHorizontally() && format.HorizontalFlip is null)
                    return $"Map format '{format.Name}' has no horizontal flip field";

                if (cell.Mirror.FlipsVertically() && format.VerticalFlip is null)
                    return $"Map format '{format.Name}' has no vertical flip field";
            }
        }

        return null;
    }

    /// <summary>
    /// Plots <paramref name="brush"/> with its top-left at the cell, clipped to the map
    /// </summary>
    /// <returns>True when a cell changed; a brush refused by <see cref="FindBrushError"/> changes nothing</returns>
    public bool Stamp(TileBrush brush, int cellX, int cellY)
    {
        if (FindBrushError(brush) is not null)
            return false;

        var changed = false;
        for (int y = 0; y < brush.Height; y++)
        {
            for (int x = 0; x < brush.Width; x++)
            {
                if (brush.GetEffectiveCell(x, y) is { } cell)
                    changed |= SetCell(cellX + x, cellY + y, cell, brush.Slot);
            }
        }

        return changed;
    }

    /// <summary>
    /// Snaps <paramref name="cell"/> to the grid of brush-sized steps from <paramref name="origin"/>, so successive stamps tile instead of overlapping
    /// </summary>
    public static Point SnapToStroke(TileBrush brush, Point origin, Point cell) =>
        BrushStroke.SnapToStroke(new Size(brush.Width, brush.Height), origin, cell);

    /// <summary>
    /// Fills <paramref name="cells"/> with <paramref name="brush"/> repeated from the rectangle's top-left, clipped to the map
    /// </summary>
    public bool Fill(TileBrush brush, Rectangle cells)
    {
        if (FindBrushError(brush) is not null)
            return false;

        var clipped = ClipToMap(cells);
        var changed = false;

        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                var brushX = BrushStroke.Mod(x - cells.Left, brush.Width);
                var brushY = BrushStroke.Mod(y - cells.Top, brush.Height);
                if (brush.GetEffectiveCell(brushX, brushY) is { } cell)
                    changed |= SetCell(x, y, cell, brush.Slot);
            }
        }

        return changed;
    }

    /// <summary>
    /// Empties the cells, so writing entries gives them the tilemap's empty entry
    /// </summary>
    public bool Erase(Rectangle cells)
    {
        var clipped = ClipToMap(cells);
        var changed = false;

        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                if (_working.GetElement(x, y) is null)
                    continue;

                _working.ResetElement(x, y);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// A brush of the cells under <paramref name="cells"/>. One cell's mirror becomes the brush flips; several cells keep their own mirrors
    /// with the flips off and take the palette slot of the first cell holding a tile. Empty cells and cells that are not tiles of the tile set are transparent.
    /// </summary>
    /// <returns>Null when no cell in the rectangle holds a tile of the tile set</returns>
    public TileBrush? Pick(Rectangle cells)
    {
        var clipped = ClipToMap(cells);
        if (clipped.Width <= 0 || clipped.Height <= 0)
            return null;

        var brushCells = new TileBrushCell?[clipped.Height, clipped.Width];
        int? slot = null;

        for (int y = 0; y < clipped.Height; y++)
        {
            for (int x = 0; x < clipped.Width; x++)
            {
                if (_working.GetElement(x + clipped.X, y + clipped.Y) is not ArrangerElement el)
                    continue;

                var tileResult = _tilemap.FindTileIndex(el.WithMirror(MirrorOperation.None));
                if (tileResult.HasFailed)
                    continue;

                brushCells[y, x] = new TileBrushCell(tileResult.AsSuccess.Result, el.Mirror);
                slot ??= _tilemap.FindCodecSlot(el.Codec, 0) ?? 0;
            }
        }

        if (slot is not int brushSlot)
            return null;

        if (brushCells.Length == 1)
        {
            var cell = brushCells[0, 0]!.Value;
            return TileBrush.Single(cell.TileIndex, brushSlot, cell.Mirror.FlipsHorizontally(), cell.Mirror.FlipsVertically());
        }

        return new TileBrush(brushCells, brushSlot);
    }

    private bool SetCell(int x, int y, TileBrushCell cell, int slot)
    {
        if (x < 0 || y < 0 || x >= MapWidth || y >= MapHeight)
            return false;

        var target = _tilemap.CreateTileElement(cell.TileIndex, SlotCodec(slot), cell.Mirror, x, y);
        if (_working.GetElement(x, y) is ArrangerElement current && IsSameTile(current, target, slot))
            return false;

        _working.SetElement(target, x, y);
        return true;
    }

    private IGraphicsCodec SlotCodec(int slot) => _slotCodecs[slot] ??= _tilemap.CloneSlotCodec(slot);

    private bool IsSameTile(ArrangerElement current, ArrangerElement target, int slot) =>
        ReferenceEquals(current.Source, target.Source) && current.SourceAddress == target.SourceAddress
        && current.Mirror == target.Mirror && current.Rotation == target.Rotation && current.Codec.Name == target.Codec.Name
        && current.Codec is IIndexedCodec { Palette: var currentPalette } && target.Codec is IIndexedCodec { Palette: var targetPalette }
        && ReferenceEquals(currentPalette, targetPalette) && _tilemap.FindCodecSlot(current.Codec, slot) == slot;

    private Rectangle ClipToMap(Rectangle cells) => Rectangle.Intersect(cells, new Rectangle(0, 0, MapWidth, MapHeight));
}
