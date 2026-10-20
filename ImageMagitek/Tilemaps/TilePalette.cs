using System;
using System.Drawing;
using ImageMagitek.Codec;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// The tiles a tilemap's brush is picked from: generated from the tile set, or a curated arranger checked cell by cell against it
/// </summary>
public sealed class TilePalette
{
    public const int DefaultColumns = 16;

    private readonly int?[,] _tileIndexes;
    private readonly string?[,] _refusals;

    /// <summary>
    /// The cells to show; tiles of the tile set use the chosen slot's palette and refused cells keep their own
    /// </summary>
    public ScatteredArranger Arranger { get; }

    public int Width => _tileIndexes.GetLength(1);
    public int Height => _tileIndexes.GetLength(0);
    public int RefusedCount { get; }

    private TilePalette(ScatteredArranger arranger, int?[,] tileIndexes, string?[,] refusals)
    {
        Arranger = arranger;
        _tileIndexes = tileIndexes;
        _refusals = refusals;

        foreach (var refusal in refusals)
        {
            if (refusal is not null)
                RefusedCount++;
        }
    }

    /// <summary>
    /// Lays out every tile of the tile set from its first tile index, <paramref name="columns"/> tiles to a row, in the palette of <paramref name="slot"/>
    /// </summary>
    public static TilePalette Generate(TilemapArranger tilemap, int slot, int columns = DefaultColumns)
    {
        var count = tilemap.TileCount;
        var rows = Math.Max(1, (count + columns - 1) / columns);
        var size = tilemap.ElementPixelSize;
        var arranger = new ScatteredArranger($"{tilemap.Name} tiles", PixelColorType.Indexed, ElementLayout.Tiled, columns, rows, size.Width, size.Height);
        var tileIndexes = new int?[rows, columns];
        var codec = tilemap.CloneSlotCodec(slot);

        for (int i = 0; i < count; i++)
        {
            int x = i % columns, y = i / columns;
            var tileIndex = tilemap.FirstTileIndex + i;
            arranger.SetElement(tilemap.CreateTileElement(tileIndex, codec, MirrorOperation.None, x, y), x, y);
            tileIndexes[y, x] = tileIndex;
        }

        return new TilePalette(arranger, tileIndexes, new string?[rows, columns]);
    }

    /// <summary>
    /// Keeps the layout of <paramref name="curated"/>. A cell is a tile when the tilemap accepts its element with rotation and mirror ignored
    /// (LIB-TILEMAPS-041) and is shown in the palette of <paramref name="slot"/>; any other element is refused with the reason.
    /// </summary>
    public static TilePalette FromArranger(TilemapArranger tilemap, ScatteredArranger curated, int slot)
    {
        var width = curated.ArrangerElementSize.Width;
        var height = curated.ArrangerElementSize.Height;
        var size = tilemap.ElementPixelSize;
        var arranger = new ScatteredArranger(curated.Name, PixelColorType.Indexed, ElementLayout.Tiled, width, height, size.Width, size.Height);
        var tileIndexes = new int?[height, width];
        var refusals = new string?[height, width];
        var codec = tilemap.CloneSlotCodec(slot);
        var sizeMatches = curated.ElementPixelSize == size;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (curated.GetElement(x, y) is not ArrangerElement el)
                    continue;

                if (!sizeMatches)
                {
                    refusals[y, x] = $"is in an arranger of {curated.ElementPixelSize.Width}x{curated.ElementPixelSize.Height} elements, not the tile set's {size.Width}x{size.Height} tiles";
                    continue;
                }

                var tileResult = tilemap.FindTileIndex(el.WithMirror(MirrorOperation.None).WithRotation(RotationOperation.None));
                if (tileResult.HasFailed)
                {
                    refusals[y, x] = tileResult.AsError.Reason;
                    if (el.Codec is IIndexedCodec)
                        arranger.SetElement(el, x, y);
                    continue;
                }

                var tileIndex = tileResult.AsSuccess.Result;
                arranger.SetElement(tilemap.CreateTileElement(tileIndex, codec, MirrorOperation.None, x, y), x, y);
                tileIndexes[y, x] = tileIndex;
            }
        }

        return new TilePalette(arranger, tileIndexes, refusals);
    }

    /// <summary>
    /// The tile index of the cell, or null when the cell is empty or refused
    /// </summary>
    public int? GetTileIndex(int x, int y) => IsInBounds(x, y) ? _tileIndexes[y, x] : null;

    /// <summary>
    /// Why the cell cannot be picked, completing "element at (x, y) ...", or null when it can or is empty
    /// </summary>
    public string? GetRefusal(int x, int y) => IsInBounds(x, y) ? _refusals[y, x] : null;

    /// <summary>
    /// A brush of the tiles in <paramref name="cells"/>, clipped to the palette; refused and empty cells become transparent
    /// </summary>
    /// <returns>Null when no cell in the rectangle is a tile</returns>
    public TileBrush? CreateBrush(Rectangle cells, int slot)
    {
        var clipped = Rectangle.Intersect(cells, new Rectangle(0, 0, Width, Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
            return null;

        var brushCells = new TileBrushCell?[clipped.Height, clipped.Width];
        var hasTile = false;

        for (int y = 0; y < clipped.Height; y++)
        {
            for (int x = 0; x < clipped.Width; x++)
            {
                if (_tileIndexes[y + clipped.Y, x + clipped.X] is not int tileIndex)
                    continue;

                brushCells[y, x] = new TileBrushCell(tileIndex, MirrorOperation.None);
                hasTile = true;
            }
        }

        return hasTile ? new TileBrush(brushCells, slot) : null;
    }

    private bool IsInBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
}
