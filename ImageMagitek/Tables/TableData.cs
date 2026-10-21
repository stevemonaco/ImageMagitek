using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Tables;

/// <summary>
/// Reads a definition's records, lookups and name tables from a data source, collecting warnings once each
/// </summary>
internal sealed class TableData
{
    private const int MaxPointerStringBytes = 256;

    private readonly HashSet<string> _warningSet = [];
    private readonly List<string> _warnings = [];
    private readonly Dictionary<(string Table, long Value), string> _names = [];

    public TableDefinition Definition { get; }
    public DataSource Source { get; }
    /// <summary>
    /// The definition's mapper resolved against the data length, so a detected copier header applies to every pointer
    /// </summary>
    public IAddressMapper Mapper { get; }
    public long Length { get; }
    public IReadOnlyList<string> Warnings => _warnings;

    public TableData(TableDefinition definition, DataSource source)
    {
        Definition = definition;
        Source = source;
        Length = source.Length;
        Mapper = definition.Mapper.ForDataLength(Length);
    }

    public void Warn(string warning)
    {
        if (_warningSet.Add(warning))
            _warnings.Add(warning);
    }

    /// <summary>
    /// Reads <paramref name="count"/> bytes, or null when they extend past the end of the data
    /// </summary>
    public byte[]? TryRead(long offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > Length)
            return null;

        return count == 0 ? [] : Source.Read(new BitAddress(offset, 0), count * 8);
    }

    public long? TryReadUnsigned(long offset, int bits, Endian endian)
    {
        var bytes = TryRead(offset, bits / 8);
        return bytes is null ? null : (long)RecordInstance.ReadBits(bytes, 0, bits, endian);
    }

    /// <exception cref="TableEvaluationException">The index is outside the lookup or its record is unreadable</exception>
    public long ReadLookup(string lookupName, long index, string field)
    {
        var lookup = Definition.Lookups[lookupName];
        if (index < 0 || index >= lookup.Count)
            throw new TableEvaluationException($"index {index} is outside lookup '{lookupName}' of {lookup.Count} entries");

        if (!Mapper.TryMap(lookup.Address, out var start))
            throw new TableEvaluationException($"lookup '{lookupName}' address 0x{lookup.Address:X} is unmapped");

        var offset = start + index * lookup.Stride;
        var bytes = TryRead(offset, lookup.Record.Size)
            ?? throw new TableEvaluationException($"lookup '{lookupName}' entry {index} at 0x{offset:X} is past the end of the data");

        var values = RecordInstance.ReadFields(lookup.Record.Fields, bytes, Mapper);
        var value = values[field];
        if (value.IsUnmapped)
            throw new TableEvaluationException($"field '{field}' of lookup '{lookupName}' entry {index} holds unmapped pointer 0x{value.Raw:X}");

        return value.Value;
    }

    /// <summary>
    /// The name a name table gives a value, with overrides, title case and the fallback of LIB-TABLES-066
    /// </summary>
    public string LookupName(NameTable table, long value)
    {
        if (_names.TryGetValue((table.Name, value), out var cached))
            return cached;

        var name = table.Overrides.TryGetValue(value, out var overrideText) ? overrideText : ReadName(table, value);
        _names[(table.Name, value)] = name;
        return name;
    }

    private string ReadName(NameTable table, long value)
    {
        var fallback = $"{table.Name} 0x{value:X}";
        var index = value - table.IndexBase;

        if (index < 0 || index >= table.Count)
        {
            Warn($"Name table '{table.Name}' has no entry for 0x{value:X}");
            return fallback;
        }

        if (!Mapper.TryMap(table.Address, out var start))
        {
            Warn($"Name table '{table.Name}' address 0x{table.Address:X} is unmapped");
            return fallback;
        }

        byte[]? bytes;
        if (table.IsFixedWidth)
        {
            bytes = TryRead(start + index * table.Width, table.Width);
        }
        else
        {
            var pointer = TryReadUnsigned(start + index * (table.PointerBits / 8), table.PointerBits, table.Endian);
            if (pointer is not { } raw || !Mapper.TryMap(raw, out var stringOffset))
            {
                Warn($"Name table '{table.Name}' entry for 0x{value:X} does not point into the data");
                return fallback;
            }

            bytes = TryRead(stringOffset, (int)Math.Min(MaxPointerStringBytes, Length - stringOffset));
        }

        if (bytes is null)
        {
            Warn($"Name table '{table.Name}' entry for 0x{value:X} is past the end of the data");
            return fallback;
        }

        var unmapped = new SortedSet<byte>();
        var text = table.Charmap.Decode(bytes, unmapped);
        if (unmapped.Count > 0)
            Warn($"Charmap '{table.Charmap.Name}' does not map bytes {string.Join(", ", unmapped.Select(x => x.ToString("X2")))} in name table '{table.Name}' entry for 0x{value:X}");

        return table.TitleCase ? NameTable.ToTitleCase(text) : text;
    }
}
