using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ImageMagitek;

/// <summary>
/// Defines a rectangular element layout for Sequential Arrangers
/// </summary>
public sealed class TileLayout
{
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public int TilesPerPattern { get; }
    public IEnumerable<Point> Pattern => _pattern;

    private List<Point> _pattern;

    public TileLayout(string name, int width, int height, int tilesPerPattern, IEnumerable<Point> pattern)
    {
        Name = name;
        Width = width;
        Height = height;
        TilesPerPattern = tilesPerPattern;
        _pattern = pattern.ToList();
    }

    public static TileLayout Default { get; } = new TileLayout("Default", 1, 1, 1, new Point[] { new Point(0, 0) });

    /// <summary>
    /// Creates a layout that visits every tile of a width x height block, row by row or column by column
    /// </summary>
    public static TileLayout Create(string name, int width, int height, bool columnMajor)
    {
        var pattern = columnMajor
            ? Enumerable.Range(0, width).SelectMany(x => Enumerable.Range(0, height).Select(y => new Point(x, y)))
            : Enumerable.Range(0, height).SelectMany(y => Enumerable.Range(0, width).Select(x => new Point(x, y)));

        return new TileLayout(name, width, height, width * height, pattern);
    }

    /// <summary>
    /// Compares name, size and pattern
    /// </summary>
    public static bool AreEquivalent(TileLayout? a, TileLayout? b)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a is null || b is null)
            return false;

        return a.Name == b.Name && a.Width == b.Width && a.Height == b.Height && a.Pattern.SequenceEqual(b.Pattern);
    }
}
