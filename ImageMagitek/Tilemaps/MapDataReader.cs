using System;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// The bytes read for a run of map entries, how many whole entries they hold, and why the source could not be read at all
/// </summary>
internal readonly record struct MapData(byte[] Data, long ReadEntries, string? SourceError);

internal static class MapDataReader
{
    /// <summary>
    /// Reads up to <paramref name="entryCount"/> entries of <paramref name="entryBytes"/> bytes at <paramref name="offset"/>, clamped to the source
    /// </summary>
    public static MapData Read(DataSource source, long offset, long entryCount, int entryBytes)
    {
        if (source is CompressedDataSource { DecompressionError: { } decompressionError })
            return new([], 0, decompressionError);

        if (source is FileDataSource { IsMissing: true })
            return new([], 0, $"Data file '{source.Name}' is missing");

        var neededBytes = entryCount * entryBytes;
        var available = Math.Max(0, source.Length - offset);
        var readBytes = (int)Math.Min(neededBytes, available);
        var data = readBytes > 0 ? source.Read(new BitAddress(offset, 0), readBytes * 8) : [];

        return new(data, readBytes / entryBytes, null);
    }
}
