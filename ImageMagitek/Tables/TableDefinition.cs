using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImageMagitek.Tables;

/// <summary>
/// A loaded, validated table definition with every expression parsed and bound (LIB-TABLES)
/// </summary>
public sealed class TableDefinition
{
    /// <summary>
    /// Full path of the definition file
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Directory holding the schemas the definition was validated with, used to validate its manifest
    /// </summary>
    public required string SchemaDirectory { get; init; }

    public required IAddressMapper Mapper { get; init; }
    public required IReadOnlyList<RecordType> RecordTypes { get; init; }
    public required IReadOnlyList<TableDeclaration> Tables { get; init; }
    public required IReadOnlyDictionary<string, LookupDeclaration> Lookups { get; init; }
    public required IReadOnlyDictionary<string, NameTable> NameTables { get; init; }
    public required IReadOnlyList<OutputDefinition> Outputs { get; init; }

    public RecordType? FindRecordType(string name) => RecordTypes.FirstOrDefault(x => x.Name == name);
    public OutputDefinition? FindOutput(string id) => Outputs.FirstOrDefault(x => x.Id == id);
}

/// <summary>
/// A bit range at a byte offset of a record; a pointer field maps through the address space and may name the record type or table it points to
/// </summary>
public sealed record FieldDefinition(string Name, int Offset, int LowBit, int Bits, Endian Endian, bool IsPointer, string? To)
{
    public int ByteCount => (LowBit + Bits + 7) / 8;
}

public sealed class RecordVariant
{
    public required string Name { get; init; }
    public required TableExpression When { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }
}

public sealed class RecordType
{
    public required string Name { get; init; }
    public required int Size { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }
    public required IReadOnlyList<RecordVariant> Variants { get; init; }

    public RecordVariant? FindVariant(string name) => Variants.FirstOrDefault(x => x.Name == name);

    /// <summary>
    /// The base fields, with a variant's fields added or replacing those of the same name
    /// </summary>
    public IReadOnlyList<FieldDefinition> FieldsWith(RecordVariant? variant)
    {
        if (variant is null)
            return Fields;

        var fields = Fields.Where(x => variant.Fields.All(v => v.Name != x.Name)).ToList();
        fields.AddRange(variant.Fields);
        return fields;
    }
}

/// <summary>
/// A table of records or of pointers, at an address (a root) or reached through pointers, with one count form
/// </summary>
public sealed class TableDeclaration
{
    public required string Name { get; init; }
    public long? Address { get; init; }

    /// <summary>
    /// The record type of each row, or null for a table of pointers
    /// </summary>
    public RecordType? Record { get; set; }

    /// <summary>
    /// For a table of pointers, the name of the record type or table each row points to
    /// </summary>
    public string? PointerTarget { get; init; }

    public int PointerBits { get; init; } = 32;
    public Endian Endian { get; init; }
    public int Stride { get; set; }
    public TableExpression? Count { get; set; }
    public TableExpression? Until { get; set; }
    public IReadOnlyList<long>? Counts { get; init; }
}

public sealed class LookupDeclaration
{
    public required string Name { get; init; }
    public required long Address { get; init; }
    public required RecordType Record { get; init; }
    public required int Count { get; init; }
    public required int Stride { get; init; }
}

public sealed class NameTable
{
    public required string Name { get; init; }
    public required long Address { get; init; }
    public required bool IsFixedWidth { get; init; }
    public int Width { get; init; }
    public int PointerBits { get; init; } = 32;
    public Endian Endian { get; init; }
    public required int Count { get; init; }
    public required Charmap Charmap { get; init; }
    public long IndexBase { get; init; }
    public bool TitleCase { get; init; }
    public required IReadOnlyDictionary<long, string> Overrides { get; init; }

    /// <summary>
    /// Upper-cases each letter not preceded by a letter or an apostrophe and lower-cases the others (LIB-TABLES-065)
    /// </summary>
    public static string ToTitleCase(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previous = ' ';

        foreach (var c in text)
        {
            if (char.IsLetter(c))
                builder.Append(char.IsLetter(previous) || previous is '\'' or '’' ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c));
            else
                builder.Append(c);

            previous = c;
        }

        return builder.ToString();
    }
}

public enum OutputType { CompressedData, Palette, MetatileSet, MetatileMap, Tilemap, Arranger }

public enum DuplicateRule { Number, Address }

public sealed record ConditionalTemplate(TableExpression? When, NameTemplate Template);

/// <summary>
/// Where a metatile set's or tilemap's tiles come from: a compressed data output of the same record, or a range of the data file
/// </summary>
public sealed record TileSource(TableExpression? When, string? OutputId, TableExpression? Offset, TableExpression? Count);

public sealed record SetReference(string OutputId, string Field, int Base);

public sealed record AttributeTableSource(TableExpression Offset, int Size);

/// <summary>
/// One kind of resource to create from each instance of a record type
/// </summary>
public sealed class OutputDefinition
{
    public required string Id { get; init; }
    public required OutputType Type { get; init; }
    public required RecordType Record { get; init; }
    public TableExpression? When { get; init; }
    public TableExpression? Repeat { get; init; }
    public RecordType? Context { get; init; }

    /// <summary>
    /// The expression whose most common value picks the context, or null to pick the first ancestor
    /// </summary>
    public TableExpression? ChooseMode { get; init; }

    public string? Variant { get; init; }
    public DuplicateRule Duplicates { get; init; }
    public required IReadOnlyList<ConditionalTemplate> Names { get; init; }
    public required IReadOnlyList<ConditionalTemplate> Folders { get; init; }

    /// <summary>
    /// Attributes holding expressions, by attribute name (offset, entries, slot, count, firsttile, width, height, tilestride, mapoffset)
    /// </summary>
    public required IReadOnlyDictionary<string, TableExpression> Expressions { get; init; }

    /// <summary>
    /// Attributes holding names or literal values, by attribute name (compression, color, format, codec, order, layout, palette, …)
    /// </summary>
    public required IReadOnlyDictionary<string, string> Values { get; init; }

    public IReadOnlyList<TileSource> Tiles { get; init; } = [];
    public string? PaletteSlotsOutput { get; init; }
    public AttributeTableSource? Attributes { get; init; }
    public IReadOnlyList<SetReference> Sets { get; init; } = [];

    public TableExpression? Expression(string attribute) => Expressions.GetValueOrDefault(attribute);
    public string? Value(string attribute) => Values.GetValueOrDefault(attribute);

    /// <summary>
    /// The root element name of the resource files this output creates
    /// </summary>
    public string TypeName => Type switch
    {
        OutputType.CompressedData => "compresseddata",
        OutputType.Palette => "palette",
        OutputType.MetatileSet => "metatileset",
        OutputType.MetatileMap => "metatilemap",
        OutputType.Tilemap => "tilemap",
        _ => "arranger"
    };
}
