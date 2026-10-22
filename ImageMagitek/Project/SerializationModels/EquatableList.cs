using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// An immutable list with sequence equality, so records holding it compare by value
/// </summary>
public sealed class EquatableList<T> : IReadOnlyList<T>, IEquatable<EquatableList<T>>
{
    private readonly T[] _items;

    private EquatableList(T[] items)
    {
        _items = items;
    }

    public static EquatableList<T> From(IEnumerable<T> items) => new(items.ToArray());

    public T this[int index] => _items[index];
    public int Count => _items.Length;

    public bool Equals(EquatableList<T>? other) =>
        other is not null && _items.SequenceEqual(other._items);

    public override bool Equals(object? obj) => Equals(obj as EquatableList<T>);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items)
            hash.Add(item);
        return hash.ToHashCode();
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
