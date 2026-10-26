using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Colors;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// Edits the entries of a tilemap's working grid, which <see cref="TilemapArranger.WriteEntries"/> later writes back:
/// plots whole tiles in Tiles mode, and pastes, applies palettes and mirrors in Arrange mode
/// </summary>
public sealed class TilemapPlotter
{
    private readonly TilemapArranger _tilemap;
    private readonly TilemapGrid _grid;

    public TilemapPlotter(TilemapArranger tilemap, TilemapGrid grid)
    {
        _tilemap = tilemap;
        _grid = grid;
    }

    private int MapWidth => _grid.Width;
    private int MapHeight => _grid.Height;

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
    /// Empties the cells that show a tile, so writing entries gives them the tilemap's empty entry
    /// </summary>
    public bool Erase(Rectangle cells)
    {
        var clipped = ClipToMap(cells);
        var changed = false;

        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                if (ShownEntry(x, y) is not null)
                    changed |= _grid.TrySet(x, y, null);
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
                if (ShownEntry(x + clipped.X, y + clipped.Y) is not { } entry)
                    continue;

                brushCells[y, x] = new TileBrushCell(entry.TileIndex, entry.Mirror);
                slot ??= entry.Palette < _tilemap.PaletteSlots.Count ? entry.Palette : 0;
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

    /// <summary>
    /// Pastes the copy's elements into the cells from <paramref name="destStart"/>; empty copied cells leave the destination unchanged.
    /// Entries copied from this tilemap keep their slot and flips; any other element is converted with the tile set's index lookup.
    /// </summary>
    /// <returns>On failure nothing changes; a refused element's reason names the tilemap and the element</returns>
    public MagitekResult Paste(ElementCopy copy, Point sourceStart, Point destStart, int width, int height)
    {
        var canCopy = ElementCopier.CanCopyElements(copy, _tilemap, sourceStart, destStart, width, height);
        if (canCopy.HasFailed)
            return canCopy;

        var sameTilemap = copy is TilemapElementCopy tilemapCopy && ReferenceEquals(tilemapCopy.ProjectResource, _tilemap) ? tilemapCopy : null;
        var cells = new List<(int X, int Y, MapEntry Entry)>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var sourceX = x + sourceStart.X;
                var sourceY = y + sourceStart.Y;
                if (copy.Elements[sourceX, sourceY] is not ArrangerElement pasted)
                    continue;

                var destX = x + destStart.X;
                var destY = y + destStart.Y;

                if (sameTilemap?.Entries[sourceX, sourceY] is { } entry)
                {
                    cells.Add((destX, destY, entry));
                    continue;
                }

                var entryResult = _tilemap.ToEntry(pasted, _grid[destX, destY]);
                if (entryResult.HasFailed)
                    return new MagitekResult.Failed($"Cannot paste into tilemap '{_tilemap.Name}': element at ({sourceX}, {sourceY}) {entryResult.AsError.Reason}");

                cells.Add((destX, destY, entryResult.AsSuccess.Result));
            }
        }

        foreach (var (x, y, entry) in cells)
            _grid.TrySet(x, y, entry);

        return MagitekResult.SuccessResult;
    }

    /// <summary>
    /// Gives the cell, when it shows a tile, the slot showing <paramref name="palette"/>, preferring the cell's own slot
    /// </summary>
    /// <returns>True when the cell changed; false when it did not or no slot shows the palette</returns>
    public bool ApplyPalette(int x, int y, Palette palette) =>
        ShownEntry(x, y) is { } entry && _tilemap.FindPaletteSlot(palette, entry.Palette) is int slot && _grid.TrySet(x, y, entry with { Palette = slot });

    /// <summary>
    /// Combines <paramref name="mirror"/> with the flips of the cell's entry
    /// </summary>
    public MagitekResult Mirror(int x, int y, MirrorOperation mirror)
    {
        if (mirror == MirrorOperation.None)
            return MagitekResult.SuccessResult;

        if (x < 0 || y < 0 || x >= MapWidth || y >= MapHeight || ShownEntry(x, y) is not { } entry)
            return new MagitekResult.Failed($"No element present at position ({x}, {y}) to be mirrored");

        var format = _tilemap.MapFormat;
        if (mirror.FlipsHorizontally() && format.HorizontalFlip is null)
            return new MagitekResult.Failed($"Map format '{format.Name}' has no horizontal flip field");

        if (mirror.FlipsVertically() && format.VerticalFlip is null)
            return new MagitekResult.Failed($"Map format '{format.Name}' has no vertical flip field");

        var combined = entry.Mirror.Combine(mirror);
        _grid.TrySet(x, y, entry with { HorizontalFlip = combined.FlipsHorizontally(), VerticalFlip = combined.FlipsVertically() });
        return MagitekResult.SuccessResult;
    }

    private bool SetCell(int x, int y, TileBrushCell cell, int slot)
    {
        if (x < 0 || y < 0 || x >= MapWidth || y >= MapHeight)
            return false;

        var current = _grid[x, y];
        var target = new MapEntry(cell.TileIndex, slot, cell.Mirror.FlipsHorizontally(), cell.Mirror.FlipsVertically(), current?.Priority ?? false);
        return _grid.TrySet(x, y, target);
    }

    private MapEntry? ShownEntry(int x, int y) =>
        _grid[x, y] is { } entry && _tilemap.IsInTileSet(entry.TileIndex) ? entry : null;

    private Rectangle ClipToMap(Rectangle cells) => Rectangle.Intersect(cells, new Rectangle(0, 0, MapWidth, MapHeight));
}
