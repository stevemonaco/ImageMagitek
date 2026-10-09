using System;
using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek;

/// <summary>
/// Lays out every palette an indexed arranger references back to back in one palette of at most 256 entries,
/// in the order the palettes first appear, so a paletted PNG keeps every color of every palette
/// </summary>
internal sealed class CombinedPalette
{
    private readonly Dictionary<Palette, (int Offset, int Count)> _slots;

    public IReadOnlyList<ColorRgba32> Colors { get; }

    private CombinedPalette(Dictionary<Palette, (int Offset, int Count)> slots, IReadOnlyList<ColorRgba32> colors)
    {
        _slots = slots;
        Colors = colors;
    }

    public static CombinedPalette? TryCreate(Arranger arranger)
    {
        if (arranger.ColorType != PixelColorType.Indexed)
            return null;

        var depths = new Dictionary<Palette, int>();
        var order = new List<Palette>();

        foreach (var codec in arranger.EnumerateElements().Select(x => x?.Codec).OfType<IIndexedCodec>())
        {
            if (codec.Palette is not { } palette)
                continue;

            if (depths.TryGetValue(palette, out var depth))
            {
                depths[palette] = Math.Max(depth, codec.ColorDepth);
            }
            else
            {
                depths[palette] = codec.ColorDepth;
                order.Add(palette);
            }
        }

        if (order.Count == 0)
            return null;

        var slots = new Dictionary<Palette, (int, int)>();
        var colors = new List<ColorRgba32>();

        foreach (var palette in order)
        {
            var count = Math.Min(palette.Entries, 1 << Math.Min(depths[palette], 8));
            if (colors.Count + count > 256)
                return null;

            slots[palette] = (colors.Count, count);
            colors.AddRange(Enumerable.Range(0, count).Select(i => palette[i]));

            if (palette.ZeroIndexTransparent && count > 0)
                colors[^count] = colors[^count] with { A = 0 };
        }

        return new CombinedPalette(slots, colors);
    }

    public bool TryGetSlot(Palette palette, out int offset, out int count)
    {
        var found = _slots.TryGetValue(palette, out var slot);
        (offset, count) = slot;
        return found;
    }

    /// <summary>
    /// True when every entry of <paramref name="source"/> matches this layout on RGB. Alpha is ignored because external editors often drop tRNS.
    /// </summary>
    public bool Matches(IReadOnlyList<ColorRgba32> source)
    {
        if (source.Count > Colors.Count)
            return false;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i].R != Colors[i].R || source[i].G != Colors[i].G || source[i].B != Colors[i].B)
                return false;
        }

        return true;
    }
}
