using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Tables;

/// <summary>
/// A record that could not be read or an output that could not be generated for it, with the reason
/// </summary>
public sealed record SkippedRecord(string Record, long? Offset, string Reason)
{
    public override string ToString() => Offset is { } offset ? $"Skipped {Record} at 0x{offset:X}: {Reason}" : $"Skipped {Record}: {Reason}";
}

/// <summary>
/// The record instances a definition reaches in a data source, in traversal order, with the records skipped and the warnings
/// </summary>
public sealed class TableEvaluation
{
    public IReadOnlyList<RecordInstance> Instances { get; }
    public IReadOnlyList<SkippedRecord> Skipped { get; }
    public IReadOnlyList<string> Warnings => Data.Warnings;
    internal TableData Data { get; }

    internal TableEvaluation(TableData data, IReadOnlyList<RecordInstance> instances, IReadOnlyList<SkippedRecord> skipped)
    {
        Data = data;
        Instances = instances;
        Skipped = skipped;
    }

    public IEnumerable<RecordInstance> InstancesOf(RecordType type) => Instances.Where(x => x.Type == type);

    /// <summary>
    /// The name a name table gives a value (LIB-TABLES-062 to -066); a fallback adds a warning
    /// </summary>
    public string LookupName(NameTable table, long value) => Data.LookupName(table, value);
}

/// <summary>
/// Supplies identifiers to expressions: a record's fields, its index and address, the repeat counter, a palette slot, a context and lookups
/// </summary>
internal sealed class EvaluationScope(TableData data) : IExpressionScope
{
    public IReadOnlyDictionary<string, FieldValue> Fields { get; init; } = new Dictionary<string, FieldValue>();
    public string Owner { get; init; } = "row";
    public long Index { get; init; }
    public long Address { get; init; }
    public long I { get; init; }
    public long? Slot { get; init; }
    public long? Value { get; init; }
    public RecordType? ContextType { get; init; }
    public RecordInstance? Context { get; init; }

    public static EvaluationScope For(TableData data, RecordInstance instance, long i = 0, long? slot = null,
        RecordType? contextType = null, RecordInstance? context = null) => new(data)
    {
        Fields = instance.Fields,
        Owner = instance.ToString(),
        Index = instance.Index,
        Address = instance.Offset,
        I = i,
        Slot = slot,
        ContextType = contextType,
        Context = context
    };

    public long GetValue(string identifier)
    {
        switch (identifier)
        {
            case "index":
                return Index;
            case "address":
                return Address;
            case "i":
                return I;
            case "slot":
                return Slot ?? throw new TableEvaluationException("'slot' is not set yet");
            case "value" when Value is not null:
                return Value.Value;
        }

        if (!Fields.TryGetValue(identifier, out var field))
            throw new TableEvaluationException($"{Owner} has no field '{identifier}'");

        if (field.IsUnmapped)
            throw new TableEvaluationException($"field '{identifier}' of {Owner} holds unmapped pointer 0x{field.Raw:X}");

        return field.Value;
    }

    public long GetContextValue(string field) => Context is not null
        ? Context.GetField(field)
        : throw new TableEvaluationException($"{Owner} has no {ContextType?.Name ?? "context"} ancestor");

    public long GetLookupValue(string lookup, long index, string field) => data.ReadLookup(lookup, index, field);

    public long MapPointer(long pointer) => data.Mapper.TryMap(pointer, out var offset)
        ? offset
        : throw new TableEvaluationException($"pointer 0x{pointer:X} is unmapped");
}

/// <summary>
/// Walks a definition's root tables over a data source depth-first, following pointers, into one instance per record type and offset
/// (LIB-TABLES-020 to -035, -100, -101)
/// </summary>
public sealed class TableEvaluator
{
    private readonly TableData _data;
    private readonly TableDefinition _definition;
    private readonly List<RecordInstance> _instances = [];
    private readonly Dictionary<(RecordType, long), RecordInstance> _byOffset = [];
    private readonly HashSet<(string, long, int, RecordInstance?)> _walkedTables = [];
    private readonly List<SkippedRecord> _skipped = [];

    public TableEvaluator(TableDefinition definition, DataSource source)
    {
        _definition = definition;
        _data = new TableData(definition, source);
    }

    public TableEvaluation Evaluate()
    {
        foreach (var table in _definition.Tables.Where(x => x.Address is not null))
        {
            if (!_data.Mapper.TryMap(table.Address!.Value, out var start))
            {
                _data.Warn($"Table '{table.Name}' address 0x{table.Address:X} is unmapped");
                continue;
            }

            WalkTable(table, start, null, 0);
        }

        return new TableEvaluation(_data, _instances, _skipped);
    }

    private long? EvaluateCount(TableDeclaration table, long start, RecordInstance? owner, int pointingIndex)
    {
        if (table.Counts is { } counts)
        {
            if (pointingIndex < counts.Count)
                return counts[pointingIndex];

            _data.Warn($"Table '{table.Name}' at 0x{start:X} has {counts.Count} counts, but row {pointingIndex} points at it");
            return 0;
        }

        if (table.Count is not { } countExpression)
            return null;

        var scope = new EvaluationScope(_data)
        {
            Fields = owner?.Fields ?? new Dictionary<string, FieldValue>(),
            Owner = owner?.ToString() ?? $"table '{table.Name}'",
            Index = pointingIndex
        };

        var result = countExpression.TryEvaluate(scope);
        if (result.HasFailed || result.AsSuccess.Result < 0)
        {
            _data.Warn($"Table '{table.Name}' at 0x{start:X} has no valid count: {(result.HasFailed ? result.AsError.Reason : $"{result.AsSuccess.Result} is negative")}");
            return 0;
        }

        return result.AsSuccess.Result;
    }

    private void WalkTable(TableDeclaration table, long start, RecordInstance? owner, int pointingIndex)
    {
        if (!_walkedTables.Add((table.Name, start, pointingIndex, owner)))
            return;

        var count = EvaluateCount(table, start, owner, pointingIndex);
        var rowSize = table.Record?.Size ?? table.PointerBits / 8;

        for (var i = 0; count is null || i < count; i++)
        {
            var rowOffset = start + (long)i * table.Stride;
            var bytes = _data.TryRead(rowOffset, rowSize);

            if (bytes is null)
            {
                _data.Warn(count is null
                    ? $"Table '{table.Name}' at 0x{start:X} reached the end of the data before its terminator after {i} rows"
                    : $"Table '{table.Name}' at 0x{start:X} row {i} of {count} is past the end of the data");
                return;
            }

            if (table.Record is { } record)
            {
                if (table.Until is { } until && IsTerminator(table, until, i, RecordInstance.ReadFields(record.Fields, bytes, _data.Mapper), null))
                    return;

                VisitRecord(record, rowOffset, owner, table.Name, i, false);
            }
            else
            {
                var raw = (long)RecordInstance.ReadBits(bytes, 0, table.PointerBits, table.Endian);
                if (table.Until is { } until && IsTerminator(table, until, i, new Dictionary<string, FieldValue>(), raw))
                    return;

                FollowPointer(table.PointerTarget!, raw, owner, table.Name, i, false);
            }
        }
    }

    private bool IsTerminator(TableDeclaration table, TableExpression until, int index, IReadOnlyDictionary<string, FieldValue> fields, long? value)
    {
        var scope = new EvaluationScope(_data) { Fields = fields, Owner = $"table '{table.Name}' row {index}", Index = index, Value = value };
        var result = until.TryEvaluate(scope);

        if (result.HasSucceeded)
            return result.AsSuccess.Result != 0;

        _data.Warn($"Table '{table.Name}' row {index}: {result.AsError.Reason}; the table ends there");
        return true;
    }

    private void FollowPointer(string target, long raw, RecordInstance? owner, string via, int index, bool viaField)
    {
        if (raw == 0)
            return;

        var from = viaField ? $"{owner} field '{via}'" : $"table '{via}' row {index}";

        if (!_data.Mapper.TryMap(raw, out var offset))
        {
            _skipped.Add(new SkippedRecord(target, null, $"{from} holds unmapped pointer 0x{raw:X}"));
            return;
        }

        if (_definition.FindRecordType(target) is { } record)
        {
            var child = VisitRecord(record, offset, owner, via, index, viaField);
            if (child is not null && viaField)
                owner?.SetChild(via, child);
        }
        else if (_definition.Tables.FirstOrDefault(x => x.Name == target) is { } table)
        {
            if (offset >= _data.Length)
                _skipped.Add(new SkippedRecord($"table '{table.Name}'", offset, $"is past the end of the data (reached from {from})"));
            else
                WalkTable(table, offset, owner, index);
        }
    }

    private RecordInstance? VisitRecord(RecordType type, long offset, RecordInstance? parent, string via, int index, bool viaField)
    {
        var reference = new RecordReference(parent, via);

        if (_byOffset.TryGetValue((type, offset), out var existing))
        {
            existing.AddReference(reference);
            return existing;
        }

        var from = viaField ? $"{parent} field '{via}'" : $"table '{via}' row {index}";
        var bytes = _data.TryRead(offset, type.Size);
        if (bytes is null)
        {
            _skipped.Add(new SkippedRecord(type.Name, offset, $"extends past the end of the data (reached from {from})"));
            return null;
        }

        var baseFields = RecordInstance.ReadFields(type.Fields, bytes, _data.Mapper);
        var variant = SelectVariant(type, offset, baseFields, index);
        var fields = variant is null ? baseFields : RecordInstance.ReadFields(type.FieldsWith(variant), bytes, _data.Mapper);

        var instance = new RecordInstance(type, offset, index, _instances.Count, variant, fields);
        instance.AddReference(reference);
        _instances.Add(instance);
        _byOffset[(type, offset)] = instance;

        foreach (var field in type.FieldsWith(variant).Where(x => x.To is not null))
        {
            var value = fields[field.Name];
            FollowPointer(field.To!, value.Raw, instance, field.Name, instance.Index, true);
        }

        return instance;
    }

    private RecordVariant? SelectVariant(RecordType type, long offset, IReadOnlyDictionary<string, FieldValue> fields, int index)
    {
        var scope = new EvaluationScope(_data) { Fields = fields, Owner = $"{type.Name} at 0x{offset:X}", Index = index, Address = offset };

        foreach (var variant in type.Variants)
        {
            var result = variant.When.TryEvaluate(scope);
            if (result.HasFailed)
            {
                _data.Warn($"{type.Name} at 0x{offset:X} variant '{variant.Name}': {result.AsError.Reason}");
                continue;
            }

            if (result.AsSuccess.Result != 0)
                return variant;
        }

        return null;
    }
}
