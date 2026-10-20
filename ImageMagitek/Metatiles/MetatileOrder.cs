using System;

namespace ImageMagitek.Metatiles;

/// <summary>
/// Order in which a metatile table stores its entries
/// <para>Interleaved stores each metatile's entries together, layer by layer, row by row</para>
/// <para>Planar stores one table of every metatile's entry per tile position of each layer</para>
/// </summary>
public enum MetatileOrder { Interleaved, Planar }

public static class MetatileOrderExtensions
{
    /// <summary>
    /// The index of the entry for tile position (<paramref name="x"/>, <paramref name="y"/>) of layer <paramref name="layer"/> of metatile
    /// <paramref name="metatile"/>, in a table of <paramref name="count"/> metatiles of <paramref name="width"/>x<paramref name="height"/> tiles
    /// </summary>
    public static long EntryIndex(this MetatileOrder order, int metatile, int layer, int x, int y, int width, int height, int layers, int count) =>
        order switch
        {
            MetatileOrder.Interleaved => ((long)metatile * layers + layer) * width * height + y * width + x,
            MetatileOrder.Planar => ((long)layer * width * height + y * width + x) * count + metatile,
            _ => throw new ArgumentOutOfRangeException(nameof(order))
        };

    public static string ToXmlName(this MetatileOrder order) => order switch
    {
        MetatileOrder.Interleaved => "interleaved",
        MetatileOrder.Planar => "planar",
        _ => throw new ArgumentOutOfRangeException(nameof(order))
    };

    public static MetatileOrder FromXmlName(string? name) => name switch
    {
        null or "interleaved" => MetatileOrder.Interleaved,
        "planar" => MetatileOrder.Planar,
        _ => throw new ArgumentException($"Unknown metatile order '{name}'", nameof(name))
    };
}
