using System;
using System.Collections.Generic;
using System.Linq;

namespace TileShop.UI.Features.Palettes;

/// <summary>
/// Selected palette indices with click, Shift+click range and Ctrl+click toggle semantics
/// </summary>
public sealed class PaletteSelection
{
    private readonly SortedSet<int> _indices = [];
    private int _anchor = -1;
    private int _focus = -1;

    public IReadOnlyCollection<int> Indices => _indices;
    public int Count => _indices.Count;

    /// <summary>
    /// Lowest selected index, or -1 when nothing is selected
    /// </summary>
    public int First => _indices.Count > 0 ? _indices.Min : -1;

    /// <summary>
    /// Most recently clicked or navigated index, or -1 when nothing is selected
    /// </summary>
    public int Focus => _focus;

    public bool Contains(int index) => _indices.Contains(index);

    public IReadOnlyList<int> ToSortedList() => _indices.ToList();

    public void Click(int index)
    {
        _indices.Clear();
        _indices.Add(index);
        _anchor = index;
        _focus = index;
    }

    public void ShiftClick(int index)
    {
        if (_anchor < 0)
        {
            Click(index);
            return;
        }

        _indices.Clear();
        for (int i = Math.Min(_anchor, index); i <= Math.Max(_anchor, index); i++)
            _indices.Add(i);
        _focus = index;
    }

    public void CtrlClick(int index)
    {
        if (!_indices.Remove(index))
            _indices.Add(index);

        _anchor = index;
        _focus = _indices.Count > 0 ? index : -1;
    }

    /// <summary>
    /// Moves the focus by a grid offset, clamped to the palette. Extending selects the range from the anchor.
    /// </summary>
    public void Move(int dx, int dy, int columns, int count, bool extend = false)
    {
        if (count <= 0 || columns <= 0)
            return;

        int from = _focus >= 0 ? _focus : 0;
        int target = Math.Clamp(from + dx + dy * columns, 0, count - 1);

        if (extend)
            ShiftClick(target);
        else
            Click(target);
    }

    /// <summary>
    /// Drops indices that no longer exist, such as after the palette shrinks
    /// </summary>
    public void Clamp(int count)
    {
        _indices.RemoveWhere(x => x >= count);

        if (_indices.Count == 0 && count > 0)
            Click(Math.Clamp(_focus, 0, count - 1));
        else if (_indices.Count == 0)
            Clear();
        else if (_focus >= count)
            _focus = _indices.Max;

        if (_anchor >= count)
            _anchor = _focus;
    }

    private void Clear()
    {
        _indices.Clear();
        _anchor = -1;
        _focus = -1;
    }
}
