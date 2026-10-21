using System;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Tables;

/// <summary>
/// Maps single bytes of a game's text encoding to text, with a terminator byte that ends a string
/// </summary>
public sealed class Charmap
{
    public string Name { get; }
    public byte Terminator { get; }
    public IReadOnlyDictionary<byte, string> Mappings { get; }

    public Charmap(string name, byte terminator, IReadOnlyDictionary<byte, string> mappings)
    {
        Name = name;
        Terminator = terminator;
        Mappings = mappings;
    }

    /// <summary>
    /// Decodes up to the terminator or the end of <paramref name="bytes"/>; an unmapped byte renders as <c>[XX]</c> and is added to <paramref name="unmapped"/>
    /// </summary>
    public string Decode(ReadOnlySpan<byte> bytes, ICollection<byte>? unmapped = null)
    {
        var builder = new StringBuilder();

        foreach (var value in bytes)
        {
            if (value == Terminator)
                break;

            if (Mappings.TryGetValue(value, out var text))
            {
                builder.Append(text);
            }
            else
            {
                builder.Append($"[{value:X2}]");
                unmapped?.Add(value);
            }
        }

        return builder.ToString();
    }
}
