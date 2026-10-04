using System;
using System.Collections.Generic;
using ColorMine.ColorSpaces;

namespace ImageMagitek.Colors;

/// <summary>
/// Outcome of matching one color against a palette
/// </summary>
/// <param name="Index">Matched entry, or the nearest candidate when the match was rejected</param>
/// <param name="Distance">Distance to the entry in the strategy's metric; 0 for an exact hit</param>
/// <param name="IsExact">True when the entry has the identical RGBA color</param>
public readonly record struct ColorMatch(byte Index, double Distance, bool IsExact);

/// <summary>
/// Matches colors to the entries of one palette using a <see cref="ColorMatchStrategy"/>, caching the result per distinct color
/// </summary>
public sealed class PaletteColorMatcher
{
    private readonly Palette _palette;
    private readonly ColorMatchStrategy _strategy;
    private readonly double? _maxDistance;
    private readonly int _entries;
    private readonly Dictionary<uint, byte> _exactLookup = new();
    private readonly Dictionary<uint, ColorMatch> _cache = new();
    private Lab[]? _labEntries;

    public Palette Palette => _palette;
    public ColorMatchStrategy Strategy => _strategy;

    /// <param name="maxDistance">Nearest strategies only: matches farther than this are rejected</param>
    /// <param name="entryLimit">Only the first entries are matchable, e.g. those a codec's color depth can store</param>
    public PaletteColorMatcher(Palette palette, ColorMatchStrategy strategy, double? maxDistance = null, int? entryLimit = null)
    {
        _palette = palette;
        _strategy = strategy;
        _maxDistance = maxDistance;
        _entries = Math.Min(palette.Entries, entryLimit ?? palette.Entries);

        for (int i = 0; i < _entries; i++)
            _exactLookup.TryAdd(palette[i].Color, (byte)i);
    }

    /// <summary>
    /// Matches a color against the palette
    /// </summary>
    /// <param name="match">The accepted match, or the nearest candidate for diagnostics when rejected</param>
    /// <returns>False when the strategy rejects every entry</returns>
    public bool TryMatch(ColorRgba32 color, out ColorMatch match)
    {
        if (!_cache.TryGetValue(color.Color, out match))
        {
            match = Match(color);
            _cache[color.Color] = match;
        }

        return IsAccepted(match);
    }

    /// <summary>
    /// True when the entry at <paramref name="index"/> is matchable and has the identical RGBA color
    /// </summary>
    public bool IsExactAt(int index, ColorRgba32 color) => index < _entries && _palette[index].Color == color.Color;

    private bool IsAccepted(ColorMatch match)
    {
        if (match.IsExact)
            return true;

        if (_strategy == ColorMatchStrategy.Exact || _entries == 0)
            return false;

        return _maxDistance is not { } max || match.Distance <= max;
    }

    private ColorMatch Match(ColorRgba32 color)
    {
        if (_exactLookup.TryGetValue(color.Color, out var exactIndex))
            return new ColorMatch(exactIndex, 0, true);

        if (_entries == 0)
            return new ColorMatch(0, double.PositiveInfinity, false);

        // Exact mode still reports a perceptual candidate so callers can suggest a fix
        return _strategy == ColorMatchStrategy.NearestRgb ? NearestRgb(color) : NearestLab(color);
    }

    private ColorMatch NearestLab(ColorRgba32 color)
    {
        _labEntries ??= BuildLabEntries();
        var lab = ColorDistance.ToLab(color);

        var minDistance = double.MaxValue;
        byte minIndex = 0;

        for (int i = 0; i < _entries; i++)
        {
            var distance = ColorDistance.Cie94(lab, _labEntries[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                minIndex = (byte)i;
            }
        }

        return new ColorMatch(minIndex, minDistance, false);
    }

    private ColorMatch NearestRgb(ColorRgba32 color)
    {
        var minDistance = double.MaxValue;
        byte minIndex = 0;

        for (int i = 0; i < _entries; i++)
        {
            var distance = ColorDistance.WeightedRgb(color, _palette[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                minIndex = (byte)i;
            }
        }

        return new ColorMatch(minIndex, minDistance, false);
    }

    private Lab[] BuildLabEntries()
    {
        var labs = new Lab[_entries];
        for (int i = 0; i < _entries; i++)
            labs[i] = ColorDistance.ToLab(_palette[i]);
        return labs;
    }
}
