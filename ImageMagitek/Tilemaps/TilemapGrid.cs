namespace ImageMagitek.Tilemaps;

/// <summary>
/// A working copy of a tilemap's entries that editing changes and <see cref="TilemapArranger.WriteEntries"/> writes back.
/// A null cell shows no tile: it is past the end of the map data, or was deleted or erased.
/// </summary>
public sealed class TilemapGrid
{
    internal MapEntry?[,] Entries { get; }

    /// <summary>
    /// Bumped on every change, so views over the grid know to rebuild their elements
    /// </summary>
    internal int Version { get; private set; }

    public int Width => Entries.GetLength(1);
    public int Height => Entries.GetLength(0);

    /// <param name="entries">Entries indexed [y, x]</param>
    internal TilemapGrid(MapEntry?[,] entries)
    {
        Entries = entries;
    }

    public MapEntry? this[int x, int y] => Entries[y, x];

    /// <returns>False when the cell already holds <paramref name="entry"/></returns>
    internal bool TrySet(int x, int y, MapEntry? entry)
    {
        if (Entries[y, x] == entry)
            return false;

        Entries[y, x] = entry;
        Version++;
        return true;
    }

    public TilemapGrid Clone() => new((MapEntry?[,])Entries.Clone());
}
