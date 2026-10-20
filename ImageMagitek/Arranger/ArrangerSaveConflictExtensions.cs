using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.ExtensionMethods;

namespace ImageMagitek;

public enum ElementSaveStatus
{
    /// <summary>
    /// Element's encoded data matches the file contents exactly
    /// </summary>
    Unchanged,

    /// <summary>
    /// Element's encoded data differs from the file, but does not conflict with any other modified element
    /// </summary>
    Modified,

    /// <summary>
    /// Element overlaps another element's file region with different encoded data, and at least one of them is modified
    /// </summary>
    Conflicting
}

/// <summary>
/// Describes the save status of a single element within an arranger
/// </summary>
/// <param name="ElementX">Element grid x-coordinate</param>
/// <param name="ElementY">Element grid y-coordinate</param>
/// <param name="Element">The arranger element</param>
/// <param name="Status">The save status of the element</param>
public readonly record struct ElementSaveState(int ElementX, int ElementY, ArrangerElement Element, ElementSaveStatus Status);

/// <summary>
/// Contains save conflict analysis results for all elements in a scattered arranger
/// </summary>
public sealed class ElementSaveConflicts
{
    public IReadOnlyList<ElementSaveState> Elements { get; }

    public bool HasConflicts => Elements.Any(e => e.Status == ElementSaveStatus.Conflicting);
    public bool HasModifications => Elements.Any(e => e.Status != ElementSaveStatus.Unchanged);

    public ElementSaveConflicts(IReadOnlyList<ElementSaveState> elements)
    {
        Elements = elements;
    }

    public IEnumerable<ElementSaveState> GetConflictingElements() =>
        Elements.Where(e => e.Status == ElementSaveStatus.Conflicting);

    public IEnumerable<ElementSaveState> GetModifiedElements() =>
        Elements.Where(e => e.Status == ElementSaveStatus.Modified);
}

public static class ScatteredArrangerSaveConflictExtensions
{
    /// <summary>
    /// Analyzes save conflicts for all elements by encoding them and comparing against current file contents
    /// </summary>
    public static ElementSaveConflicts AnalyzeSaveConflicts(this ScatteredArranger arranger, IndexedImage image)
    {
        var encodedElements = EncodeAllElements(arranger, image);
        return AnalyzeSaveConflicts(arranger, encodedElements);
    }

    /// <summary>
    /// Analyzes save conflicts for all elements by encoding them and comparing against current file contents
    /// </summary>
    public static ElementSaveConflicts AnalyzeSaveConflicts(this ScatteredArranger arranger, DirectImage image)
    {
        var encodedElements = EncodeAllElements(arranger, image);
        return AnalyzeSaveConflicts(arranger, encodedElements);
    }

    /// <summary>
    /// Analyzes save conflicts for each element by comparing encoded image data against current file contents
    /// </summary>
    /// <param name="arranger">The arranger to analyze</param>
    /// <param name="encodedElements">Encoded data per element, keyed by (elemX, elemY) grid position</param>
    public static ElementSaveConflicts AnalyzeSaveConflicts(this ScatteredArranger arranger, Dictionary<(int X, int Y), byte[]> encodedElements)
    {
        var results = new List<ElementSaveState>();
        var ranges = new List<(DataSource Source, long StartBit, long EndBit, int ElemX, int ElemY, byte[] Data, bool IsModified)>();

        // First pass: determine which elements are modified by comparing encoded data to file contents
        for (int y = 0; y < arranger.ArrangerElementSize.Height; y++)
        {
            for (int x = 0; x < arranger.ArrangerElementSize.Width; x++)
            {
                var el = arranger.GetElement(x, y);
                if (el is not ArrangerElement element)
                    continue;

                if (!encodedElements.TryGetValue((x, y), out var encodedData))
                    continue;

                var fileData = ReadElementFromFile(element);
                bool isModified = fileData is null || !encodedData.AsSpan().SequenceEqual(fileData);

                long startBit = element.SourceAddress.Offset;
                long endBit = startBit + element.Codec.StorageSize;
                ranges.Add((element.Source, startBit, endBit, x, y, encodedData, isModified));

                results.Add(new ElementSaveState(x, y, element, isModified ? ElementSaveStatus.Modified : ElementSaveStatus.Unchanged));
            }
        }

        // Second pass: every element is written on save, so an unchanged element overlapping a modified one can overwrite it
        for (int i = 0; i < ranges.Count; i++)
        {
            for (int j = i + 1; j < ranges.Count; j++)
            {
                var a = ranges[i];
                var b = ranges[j];

                if (!(a.IsModified || b.IsModified) || !ReferenceEquals(a.Source, b.Source))
                    continue;

                bool writesSameBits = a.StartBit == b.StartBit && a.EndBit == b.EndBit && a.Data.AsSpan().SequenceEqual(b.Data);

                if (!writesSameBits && a.StartBit < b.EndBit && b.StartBit < a.EndBit)
                {
                    UpgradeStatus(results, a.ElemX, a.ElemY, ElementSaveStatus.Conflicting);
                    UpgradeStatus(results, b.ElemX, b.ElemY, ElementSaveStatus.Conflicting);
                }
            }
        }

        return new ElementSaveConflicts(results);
    }

    private static Dictionary<(int X, int Y), byte[]> EncodeAllElements(ScatteredArranger arranger, IndexedImage image)
    {
        var fullImage = arranger.CopyPixelsIndexed().Image;
        var buffer = new byte[arranger.ElementPixelSize.Height, arranger.ElementPixelSize.Width];
        var result = new Dictionary<(int X, int Y), byte[]>();

        // Merge edited image into full arranger image
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                fullImage.Image[(y + image.Top) * fullImage.Width + x + image.Left] = image.Image[y * image.Width + x];

        for (int ey = 0; ey < arranger.ArrangerElementSize.Height; ey++)
        {
            for (int ex = 0; ex < arranger.ArrangerElementSize.Width; ex++)
            {
                var el = arranger.GetElement(ex, ey);
                if (el is ArrangerElement element && element.Codec is IIndexedCodec codec)
                {
                    fullImage.Image.CopyToArray2D(element.X1, element.Y1, fullImage.Width, buffer,
                        0, 0, arranger.ElementPixelSize.Width, arranger.ElementPixelSize.Height);

                    buffer.InverseMirrorArray2D(element.Mirror);
                    buffer.InverseRotateArray2D(element.Rotation);

                    var encoded = codec.EncodeElement(element, buffer);
                    result[(ex, ey)] = encoded.ToArray();
                }
            }
        }

        return result;
    }

    private static Dictionary<(int X, int Y), byte[]> EncodeAllElements(ScatteredArranger arranger, DirectImage image)
    {
        var fullImage = image.MergeIntoArrangerImage();
        var fullWidth = arranger.ArrangerPixelSize.Width;
        var buffer = new ColorRgba32[arranger.ElementPixelSize.Height, arranger.ElementPixelSize.Width];
        var result = new Dictionary<(int X, int Y), byte[]>();

        for (int ey = 0; ey < arranger.ArrangerElementSize.Height; ey++)
        {
            for (int ex = 0; ex < arranger.ArrangerElementSize.Width; ex++)
            {
                var el = arranger.GetElement(ex, ey);
                if (el is ArrangerElement element && element.Codec is IDirectCodec codec)
                {
                    fullImage.CopyToArray2D(buffer, element.X1, element.Y1, fullWidth, element.Width, element.Height);

                    buffer.InverseMirrorArray2D(element.Mirror);
                    buffer.InverseRotateArray2D(element.Rotation);

                    var encoded = codec.EncodeElement(element, buffer);
                    result[(ex, ey)] = encoded.ToArray();
                }
            }
        }

        return result;
    }

    private static byte[]? ReadElementFromFile(ArrangerElement element)
    {
        var storageSize = element.Codec.StorageSize;
        if (element.SourceAddress.Offset + storageSize > element.Source.Length * 8)
            return null;

        var buffer = new byte[(storageSize + 7) / 8];
        element.Source.Read(element.SourceAddress, storageSize, buffer);
        return buffer;
    }

    private static void UpgradeStatus(List<ElementSaveState> results, int elemX, int elemY, ElementSaveStatus newStatus)
    {
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].ElementX == elemX && results[i].ElementY == elemY && results[i].Status < newStatus)
            {
                results[i] = results[i] with { Status = newStatus };
                return;
            }
        }
    }
}

/// <summary>
/// A tile read by several cells whose pixels differ between them, so it was left unchanged
/// </summary>
/// <param name="Cells">Element grid locations of every cell that reads the tile, in row-major order</param>
public sealed record SharedTileConflict(BitAddress Address, IReadOnlyList<Point> Cells);

public static class SharedTileExtensions
{
    /// <summary>
    /// Makes the cells of <paramref name="result"/> that read the same tile agree, comparing their pixels with mirror and rotation undone.
    /// Both images must cover the whole arranger.
    /// </summary>
    /// <param name="current">The pixels the data currently holds</param>
    /// <param name="result">The edited pixels; agreed tiles are copied into every cell that reads them, and conflicting cells are reset to <paramref name="current"/></param>
    /// <param name="changedCellsWin">When true, only cells that differ from <paramref name="current"/> must agree, and their tile replaces the others.
    /// When false, every cell of the tile must agree.</param>
    /// <returns>The tiles left unchanged because their cells disagree</returns>
    public static IReadOnlyList<SharedTileConflict> ResolveSharedTiles(this Arranger arranger, IndexedImage current, IndexedImage result, bool changedCellsWin)
    {
        var groups = arranger.EnumerateElementsWithinElementRange()
            .Select(p => (Cell: new Point(p.x, p.y), Element: arranger.GetElement(p.x, p.y)))
            .Where(c => c.Element is not null)
            .Select(c => (c.Cell, Element: c.Element!.Value))
            .GroupBy(c => (c.Element.Source, c.Element.SourceAddress))
            .Where(g => g.Count() > 1);

        var conflicts = new List<SharedTileConflict>();
        foreach (var cells in groups)
        {
            var tiles = cells.Select(c => (c.Element, Current: ReadTile(current, c.Element), Result: ReadTile(result, c.Element))).ToList();
            var candidates = changedCellsWin ? tiles.Where(t => !AreEqual(t.Current, t.Result)).ToList() : tiles;
            if (candidates.Count == 0)
                continue;

            var agreed = candidates[0].Result;
            if (candidates.All(t => AreEqual(t.Result, agreed)))
            {
                foreach (var tile in tiles)
                    WriteTile(result, tile.Element, agreed);
            }
            else
            {
                foreach (var tile in tiles)
                    WriteTile(result, tile.Element, tile.Current);

                conflicts.Add(new SharedTileConflict(cells.Key.SourceAddress, cells.Select(c => c.Cell).ToList()));
            }
        }

        return conflicts;
    }

    private static byte[,] ReadTile(IndexedImage image, ArrangerElement element)
    {
        var tile = new byte[element.Height, element.Width];
        image.Image.CopyToArray2D(element.X1, element.Y1, image.Width, tile, 0, 0, element.Width, element.Height);
        tile.InverseMirrorArray2D(element.Mirror);
        tile.InverseRotateArray2D(element.Rotation);
        return tile;
    }

    private static void WriteTile(IndexedImage image, ArrangerElement element, byte[,] tile)
    {
        var cell = (byte[,])tile.Clone();
        cell.RotateArray2D(element.Rotation);
        cell.MirrorArray2D(element.Mirror);

        for (int y = 0; y < element.Height; y++)
        {
            for (int x = 0; x < element.Width; x++)
                image.Image[(element.Y1 + y) * image.Width + element.X1 + x] = cell[y, x];
        }
    }

    private static bool AreEqual(byte[,] a, byte[,] b) => a.Cast().SequenceEqual(b.Cast());
}
