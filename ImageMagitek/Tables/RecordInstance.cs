using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Tables;

/// <summary>
/// How a record instance was reached: from a parent instance's field or table row, or from a root table
/// </summary>
/// <param name="Parent">The instance whose pointer reached this one, or null from a root table</param>
/// <param name="Via">The field or table name the reference went through</param>
public sealed record RecordReference(RecordInstance? Parent, string Via);

/// <summary>
/// The value of one field: a pointer field holds its mapped offset, 0 when null, or is unmapped
/// </summary>
public readonly record struct FieldValue(long Value, long Raw, bool IsUnmapped);

/// <summary>
/// One record of a record type at one file offset, however many rows and pointers reach it (LIB-TABLES-100)
/// </summary>
public sealed class RecordInstance
{
    private readonly List<RecordReference> _references = [];
    private readonly Dictionary<string, RecordInstance> _children = [];

    public RecordType Type { get; }
    public long Offset { get; }

    /// <summary>
    /// The row index of the table row that first reached the record; a record reached through a pointer field takes its parent's
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Position in traversal order among all instances
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// The record path of its first reference, from a root table row (LIB-TABLES-160)
    /// </summary>
    public string Path { get; }

    public RecordVariant? Variant { get; }
    public IReadOnlyDictionary<string, FieldValue> Fields { get; }
    public IReadOnlyList<RecordReference> References => _references;

    /// <summary>
    /// The instances reached through this record's pointer fields to record types, by field name
    /// </summary>
    public IReadOnlyDictionary<string, RecordInstance> Children => _children;

    internal RecordInstance(RecordType type, long offset, int index, int order, RecordVariant? variant, IReadOnlyDictionary<string, FieldValue> fields, string path)
    {
        Type = type;
        Offset = offset;
        Index = index;
        Order = order;
        Variant = variant;
        Fields = fields;
        Path = path;
    }

    internal void AddReference(RecordReference reference) => _references.Add(reference);
    internal void SetChild(string field, RecordInstance child) => _children[field] = child;

    /// <exception cref="TableEvaluationException">The field holds an unmapped pointer or does not exist</exception>
    public long GetField(string name)
    {
        if (!Fields.TryGetValue(name, out var value))
            throw new TableEvaluationException($"{Type.Name} at 0x{Offset:X} has no field '{name}'");

        if (value.IsUnmapped)
            throw new TableEvaluationException($"field '{name}' of {Type.Name} at 0x{Offset:X} holds unmapped pointer 0x{value.Raw:X}");

        return value.Value;
    }

    /// <summary>
    /// The distinct ancestors of a record type, walking references up, ordered by when they were first reached
    /// </summary>
    public IReadOnlyList<RecordInstance> FindAncestors(RecordType type)
    {
        var visited = new HashSet<RecordInstance>();
        var found = new List<RecordInstance>();
        var pending = new Queue<RecordInstance>([this]);

        while (pending.Count > 0)
        {
            foreach (var parent in pending.Dequeue().References.Select(x => x.Parent).OfType<RecordInstance>())
            {
                if (!visited.Add(parent))
                    continue;

                if (parent.Type == type)
                    found.Add(parent);
                else
                    pending.Enqueue(parent);
            }
        }

        found.Sort((a, b) => a.Order.CompareTo(b.Order));
        return found;
    }

    public override string ToString() => $"{Type.Name} at 0x{Offset:X}";

    internal static IReadOnlyDictionary<string, FieldValue> ReadFields(IEnumerable<FieldDefinition> fields, ReadOnlySpan<byte> bytes, IAddressMapper mapper)
    {
        var values = new Dictionary<string, FieldValue>();

        foreach (var field in fields)
        {
            var raw = (long)ReadBits(bytes.Slice(field.Offset, field.ByteCount), field.LowBit, field.Bits, field.Endian);

            if (!field.IsPointer)
                values[field.Name] = new(raw, raw, false);
            else if (raw == 0)
                values[field.Name] = new(0, 0, false);
            else if (mapper.TryMap(raw, out var offset))
                values[field.Name] = new(offset, raw, false);
            else
                values[field.Name] = new(0, raw, true);
        }

        return values;
    }

    /// <summary>
    /// Assembles <paramref name="bytes"/> in <paramref name="endian"/> order, shifts right by <paramref name="lowBit"/> and masks to <paramref name="bits"/>
    /// </summary>
    public static ulong ReadBits(ReadOnlySpan<byte> bytes, int lowBit, int bits, Endian endian)
    {
        UInt128 assembled = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = endian == Endian.Little ? bytes[i] : bytes[bytes.Length - 1 - i];
            assembled |= (UInt128)b << (8 * i);
        }

        var shifted = assembled >> lowBit;
        var mask = bits == 64 ? ulong.MaxValue : (1UL << bits) - 1;
        return (ulong)(shifted & mask);
    }
}
