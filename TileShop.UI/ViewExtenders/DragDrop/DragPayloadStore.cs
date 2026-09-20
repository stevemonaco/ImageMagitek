using System;
using System.Collections.Concurrent;

namespace TileShop.UI.DragDrop;

/// <summary>
/// Holds drag payloads in-process and exchanges them for a key that a drag operation can carry.
/// </summary>
/// <remarks>
/// Avalonia's drag-and-drop transfers only strings and bytes, so arbitrary payload objects have to be
/// looked up by key on the drop side.
/// </remarks>
internal static class DragPayloadStore
{
    private static readonly ConcurrentDictionary<string, object> _payloads = new();

    public static string Add(object payload)
    {
        var key = Guid.NewGuid().ToString("N");
        _payloads[key] = payload;
        return key;
    }

    public static object? Get(string? key) =>
        key is not null && _payloads.TryGetValue(key, out var payload) ? payload : null;

    public static void Remove(string? key)
    {
        if (key is not null)
            _payloads.TryRemove(key, out _);
    }
}
