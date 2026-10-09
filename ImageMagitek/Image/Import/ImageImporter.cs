using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace ImageMagitek.Image.Import;

/// <summary>
/// Stages image files for import into arrangers, matching colors to palettes and reporting the outcome
/// </summary>
public static class ImageImporter
{
    public static MagitekResult<ImageImportPreview> Prepare(Arranger arranger, string imagePath, ImageImportOptions options, IImageFileAdapter adapter)
    {
        return adapter.LoadImage(imagePath).Match(
            success => Prepare(arranger, success.Result, options),
            fail => new MagitekResult<ImageImportPreview>.Failed(fail.Reason));
    }

    /// <summary>
    /// Stages <paramref name="source"/> onto the arranger. Arranger pixels not covered by the image or outside
    /// <paramref name="bounds"/> are left unchanged, and image pixels outside the arranger are cropped.
    /// </summary>
    /// <param name="offset">Arranger pixel where the image's top-left pixel lands; may be negative</param>
    /// <param name="bounds">Arranger pixels that may change; defaults to the whole arranger</param>
    public static MagitekResult<ImageImportPreview> Prepare(Arranger arranger, DecodedImage source, ImageImportOptions options,
        Point offset = default, Rectangle? bounds = null)
    {
        if (arranger.GetReadOnlyReason() is { } reason)
            return new MagitekResult<ImageImportPreview>.Failed($"Arranger '{arranger.Name}' is read-only because it {reason}");

        var size = arranger.ArrangerPixelSize;
        var region = Rectangle.Intersect(bounds ?? new Rectangle(Point.Empty, size), new Rectangle(Point.Empty, size));

        return arranger.ColorType switch
        {
            PixelColorType.Indexed => new MagitekResult<ImageImportPreview>.Success(PrepareIndexed(arranger, source, options, offset, region)),
            PixelColorType.Direct => new MagitekResult<ImageImportPreview>.Success(PrepareDirect(arranger, source, offset, region)),
            _ => new MagitekResult<ImageImportPreview>.Failed($"Arranger '{arranger.Name}' has an unsupported color type '{arranger.ColorType}'")
        };
    }

    private static ImageImportPreview PrepareIndexed(Arranger arranger, DecodedImage source, ImageImportOptions options, Point offset, Rectangle region)
    {
        var current = new IndexedImage(arranger);
        var result = new IndexedImage(arranger);
        var states = new ImportPixelState[current.Image.Length];
        var combined = source.Indices is not null && source.Palette is not null && CombinedPalette.TryCreate(arranger) is { } layout && layout.Matches(source.Palette)
            ? layout
            : null;
        var sourceIndices = combined is not null ? source.Indices : null;
        var usedSourceIndices = false;

        var matchers = new Dictionary<(Palette, int), PaletteColorMatcher>();
        var substitutions = new Dictionary<(uint, Palette, byte), SubstitutionAccumulator>();
        var unmatched = new Dictionary<(uint, Palette), UnmatchedAccumulator>();

        foreach (var (x, y, i, si) in EnumeratePixels(source, current.Width, offset, region))
        {
            if (arranger.GetElementAtPixel(x, y)?.Codec is not IIndexedCodec codec)
                continue;

            var palette = codec.Palette;
            var color = source.Pixels[si];
            var state = ImportPixelState.Unchanged;
            byte index;

            if (TryGetLocalIndex(sourceIndices, si, combined, palette, codec.ColorDepth, out var localIndex))
            {
                index = localIndex;
                usedSourceIndices = true;
            }
            else if (options.MapTransparentToIndexZero && color.A <= options.AlphaThreshold)
            {
                index = 0;
            }
            else
            {
                var matcher = GetMatcher(matchers, palette, codec.ColorDepth, options);
                var currentIndex = current.Image[i];

                // Duplicate palette colors make an exported pixel ambiguous; keeping its current index avoids spurious changes
                if (matcher.IsExactAt(currentIndex, color))
                {
                    index = currentIndex;
                }
                else if (matcher.TryMatch(color, out var match))
                {
                    index = match.Index;

                    if (!match.IsExact)
                    {
                        state |= ImportPixelState.Substituted;
                        Accumulate(substitutions, color, palette, match, x, y);
                    }
                }
                else
                {
                    states[i] = ImportPixelState.Unmatched;
                    Accumulate(unmatched, color, palette, match, x, y);
                    continue;
                }
            }

            result.Image[i] = index;

            if (index != current.Image[i])
                state |= ImportPixelState.Changed;

            states[i] = state;
        }

        var report = new ImportReport(current.Width, current.Height, states,
            substitutions.Select(kv => new ColorMatchEntry(new ColorRgba32(kv.Key.Item1), kv.Key.Item2, kv.Value.Index, kv.Value.Distance, kv.Value.Count, kv.Value.First))
                .OrderByDescending(x => x.Distance).ThenByDescending(x => x.Count).ToList(),
            unmatched.Select(kv => new UnmatchedColorEntry(new ColorRgba32(kv.Key.Item1), kv.Key.Item2, kv.Value.Count, kv.Value.First, kv.Value.NearestIndex, kv.Value.NearestDistance))
                .OrderByDescending(x => x.Count).ToList(),
            usedSourceIndices);

        return new ImageImportPreview(arranger, source, report, current, result) { Offset = offset, Bounds = region };
    }

    /// <summary>
    /// A paletted source's index is authoritative when it falls within the range of the pixel's own palette.
    /// An index from another palette's range was painted with a color the element can't use, so it gets color matched instead.
    /// </summary>
    private static bool TryGetLocalIndex(byte[]? sourceIndices, int si, CombinedPalette? combined, Palette palette, int colorDepth, out byte localIndex)
    {
        localIndex = 0;

        if (sourceIndices is null || combined is null || !combined.TryGetSlot(palette, out var offset, out var count))
            return false;

        var local = sourceIndices[si] - offset;
        if (local < 0 || local >= count || local >= (1 << colorDepth))
            return false;

        localIndex = (byte)local;
        return true;
    }

    private static ImageImportPreview PrepareDirect(Arranger arranger, DecodedImage source, Point offset, Rectangle region)
    {
        var current = new DirectImage(arranger);
        var result = new DirectImage(arranger);
        var states = new ImportPixelState[current.Image.Length];

        foreach (var (x, y, i, si) in EnumeratePixels(source, current.Width, offset, region))
        {
            if (arranger.GetElementAtPixel(x, y)?.Codec is not IDirectCodec)
                continue;

            var color = source.Pixels[si];
            result.Image[i] = color;

            if (color.Color != current.Image[i].Color)
                states[i] = ImportPixelState.Changed;
        }

        var report = new ImportReport(current.Width, current.Height, states, [], []);
        return new ImageImportPreview(arranger, source, report, current, result) { Offset = offset, Bounds = region };
    }

    /// <summary>
    /// Arranger pixels within the region that the source covers, with arranger and source indices
    /// </summary>
    private static IEnumerable<(int X, int Y, int ArrangerIndex, int SourceIndex)> EnumeratePixels(DecodedImage source, int arrangerWidth, Point offset, Rectangle region)
    {
        var left = Math.Max(region.Left, offset.X);
        var top = Math.Max(region.Top, offset.Y);
        var right = Math.Min(region.Right, offset.X + source.Width);
        var bottom = Math.Min(region.Bottom, offset.Y + source.Height);

        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
                yield return (x, y, y * arrangerWidth + x, (y - offset.Y) * source.Width + (x - offset.X));
        }
    }

    private static PaletteColorMatcher GetMatcher(Dictionary<(Palette, int), PaletteColorMatcher> matchers, Palette palette, int colorDepth, ImageImportOptions options)
    {
        var entryLimit = 1 << colorDepth;

        if (!matchers.TryGetValue((palette, entryLimit), out var matcher))
        {
            matcher = new PaletteColorMatcher(palette, options.MatchStrategy, options.MaxDistance, entryLimit);
            matchers[(palette, entryLimit)] = matcher;
        }

        return matcher;
    }

    private static void Accumulate(Dictionary<(uint, Palette, byte), SubstitutionAccumulator> substitutions, ColorRgba32 color, Palette palette, ColorMatch match, int x, int y)
    {
        var key = (color.Color, palette, match.Index);

        if (!substitutions.TryGetValue(key, out var entry))
        {
            entry = new SubstitutionAccumulator { Index = match.Index, Distance = match.Distance, First = new Point(x, y) };
            substitutions[key] = entry;
        }

        entry.Count++;
    }

    private static void Accumulate(Dictionary<(uint, Palette), UnmatchedAccumulator> unmatched, ColorRgba32 color, Palette palette, ColorMatch nearest, int x, int y)
    {
        var key = (color.Color, palette);

        if (!unmatched.TryGetValue(key, out var entry))
        {
            entry = new UnmatchedAccumulator { NearestIndex = nearest.Index, NearestDistance = nearest.Distance, First = new Point(x, y) };
            unmatched[key] = entry;
        }

        entry.Count++;
    }

    private sealed class SubstitutionAccumulator
    {
        public byte Index;
        public double Distance;
        public int Count;
        public Point First;
    }

    private sealed class UnmatchedAccumulator
    {
        public byte NearestIndex;
        public double NearestDistance;
        public int Count;
        public Point First;
    }
}
