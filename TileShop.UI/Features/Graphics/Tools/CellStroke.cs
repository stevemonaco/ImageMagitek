using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TileShop.Shared.Tools;

namespace TileShop.UI.Features.Graphics.Tools;

/// <summary>
/// The state of a cell plotting stroke: its name, whether it changed a cell, its snapping origin and the last cell it reached
/// </summary>
public sealed class CellStroke
{
    private bool _failureShown;
    private Point? _lastCell;

    /// <summary>
    /// The history step name of the active stroke, or null when no stroke is active
    /// </summary>
    public string? Name { get; private set; }

    public bool Changed { get; set; }
    public Point? Origin { get; set; }

    public void Begin(string name)
    {
        Name = name;
        _failureShown = false;
    }

    /// <summary>
    /// Ends the stroke
    /// </summary>
    /// <returns>The stroke's name when it changed a cell, otherwise null</returns>
    public string? End()
    {
        var recorded = Changed ? Name : null;
        Name = null;
        Changed = false;
        Origin = null;
        _lastCell = null;
        return recorded;
    }

    /// <summary>
    /// The cells from the previous stroke cell to <paramref name="cell"/>, excluding the previous one
    /// </summary>
    public IEnumerable<Point> Advance(Point cell)
    {
        var cells = _lastCell is { } last ? PixelLine.Points(last, cell).Skip(1).ToList() : [cell];
        _lastCell = cell;
        return cells;
    }

    /// <summary>
    /// Forgets the last stroke cell, so a pointer re-entering the image does not plot across the gap
    /// </summary>
    public void Break() => _lastCell = null;

    /// <summary>
    /// Whether a plotting failure should be reported: always outside a stroke, once per stroke within one
    /// </summary>
    public bool ShouldReport()
    {
        if (_failureShown)
            return false;

        _failureShown = Name is not null;
        return true;
    }
}
