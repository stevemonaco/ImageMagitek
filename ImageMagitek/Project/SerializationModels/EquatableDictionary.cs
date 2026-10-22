using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// An immutable dictionary that is equal to another holding the same entries in any order, so records holding it compare by value
/// </summary>
public sealed class EquatableDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>, IEquatable<EquatableDictionary<TKey, TValue>>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items;

    private EquatableDictionary(Dictionary<TKey, TValue> items)
    {
        _items = items;
    }

    public static EquatableDictionary<TKey, TValue> From(IEnumerable<KeyValuePair<TKey, TValue>> items) => new(new Dictionary<TKey, TValue>(items));

    public TValue this[TKey key] => _items[key];
    public IEnumerable<TKey> Keys => _items.Keys;
    public IEnumerable<TValue> Values => _items.Values;
    public int Count => _items.Count;

    public bool ContainsKey(TKey key) => _items.ContainsKey(key);
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _items.TryGetValue(key, out value);

    public bool Equals(EquatableDictionary<TKey, TValue>? other) =>
        other is not null && other.Count == Count &&
            _items.All(x => other._items.TryGetValue(x.Key, out var value) && EqualityComparer<TValue>.Default.Equals(x.Value, value));

    public override bool Equals(object? obj) => Equals(obj as EquatableDictionary<TKey, TValue>);

    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var (key, value) in _items)
            hash ^= HashCode.Combine(key, value);
        return hash;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
