using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Colors;

/// <summary>
/// A palette entry whose raw value is assembled from bit fields within bytes of the palette's data file
/// </summary>
public class ScatteredColorSource : IColorSource
{
    public IReadOnlyList<ColorField> Fields { get; }

    /// <summary>
    /// Raw value bits that some field supplies
    /// </summary>
    public uint CoveredBits { get; }

    /// <exception cref="ColorFieldException">A field is malformed or overlaps another field</exception>
    public ScatteredColorSource(IEnumerable<ColorField> fields)
    {
        var array = fields.ToArray();
        if (array.Length == 0)
            throw new ColorFieldException(null, 0, "a scattered color needs at least one field");

        uint covered = 0;
        for (int i = 0; i < array.Length; i++)
        {
            var f = array[i];
            if (f.LowBit is < 0 or > 7)
                throw new ColorFieldException(null, i, $"low bit {f.LowBit} is outside 0-7");
            if (f.Bits is < 1 or > 8)
                throw new ColorFieldException(null, i, $"bits {f.Bits} is outside 1-8");
            if (f.LowBit + f.Bits > 8)
                throw new ColorFieldException(null, i, $"low bit {f.LowBit} plus bits {f.Bits} exceeds 8");
            if (f.Shift is < 0 or > 31)
                throw new ColorFieldException(null, i, $"shift {f.Shift} is outside 0-31");
            if (f.Offset < 0)
                throw new ColorFieldException(null, i, $"offset {f.Offset} is negative");
            if ((covered & f.ValueMask) != 0)
                throw new ColorFieldException(null, i, "overlaps another field's value bits");

            for (int j = 0; j < i; j++)
            {
                var g = array[j];
                if (g.Offset == f.Offset && g.LowBit < f.LowBit + f.Bits && f.LowBit < g.LowBit + g.Bits)
                    throw new ColorFieldException(null, i, $"overlaps field {j}'s bits in the file");
            }

            covered |= f.ValueMask;
        }

        Fields = array;
        CoveredBits = covered;
    }

    /// <summary>
    /// Builds the sources of a run of <paramref name="entries"/> entries, entry n's fields advanced n strides from the first
    /// </summary>
    /// <exception cref="ColorFieldException">A field crosses a byte for some entry, or an entry's fields are invalid</exception>
    public static List<ScatteredColorSource> ExpandRun(int entries, IReadOnlyList<ColorFieldRun> runs)
    {
        var result = new List<ScatteredColorSource>(entries);

        for (int n = 0; n < entries; n++)
        {
            var fields = new ColorField[runs.Count];
            for (int k = 0; k < runs.Count; k++)
            {
                var first = runs[k].First;
                var position = first.BitPosition + (long)n * runs[k].Stride;
                var lowBit = (int)(position % 8);

                if (lowBit + first.Bits > 8)
                    throw new ColorFieldException(n, k, "crosses a byte boundary");

                fields[k] = first with { Offset = position / 8, LowBit = lowBit };
            }

            try
            {
                result.Add(new ScatteredColorSource(fields));
            }
            catch (ColorFieldException ex)
            {
                throw ex.ForEntry(n);
            }
        }

        return result;
    }
}

/// <summary>
/// A scattered color field that is invalid, identified by its index and, when known, its entry within a run
/// </summary>
public class ColorFieldException : ArgumentException
{
    public int? Entry { get; }
    public int Field { get; }
    public string Reason { get; }

    public ColorFieldException(int? entry, int field, string reason)
        : base(entry is null ? $"Field {field}: {reason}" : $"Entry {entry} field {field}: {reason}")
    {
        Entry = entry;
        Field = field;
        Reason = reason;
    }

    public ColorFieldException ForEntry(int entry) => new(entry, Field, Reason);
}
