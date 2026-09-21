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

    public static MagitekResult<ImageImportPreview> Prepare(Arranger arranger, DecodedImage source, ImageImportOptions options)
    {
        var size = arranger.ArrangerPixelSize;

        if (source.Width != size.Width || source.Height != size.Height)
        {
            return new MagitekResult<ImageImportPreview>.Failed(
                $"Arranger dimensions ({size.Width}, {size.Height}) do not match image dimensions ({source.Width}, {source.Height})");
        }

        return arranger.ColorType switch
        {
            PixelColorType.Indexed => new MagitekResult<ImageImportPreview>.Success(PrepareIndexed(arranger, source, options)),
            PixelColorType.Direct => new MagitekResult<ImageImportPreview>.Success(PrepareDirect(arranger, source)),
            _ => new MagitekResult<ImageImportPreview>.Failed($"Arranger '{arranger.Name}' has an unsupported color type '{arranger.ColorType}'")
        };
    }

    private static ImageImportPreview PrepareIndexed(Arranger arranger, DecodedImage source, ImageImportOptions options)
    {
        var current = new IndexedImage(arranger);
        var result = new IndexedImage(arranger);
        var states = new ImportPixelState[source.Pixels.Length];

        var matchers = new Dictionary<(Palette, int), PaletteColorMatcher>();
        var substitutions = new Dictionary<(uint, Palette, byte), SubstitutionAccumulator>();
        var unmatched = new Dictionary<(uint, Palette), UnmatchedAccumulator>();

        for (int y = 0, i = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++, i++)
            {
                if (arranger.GetElementAtPixel(x, y)?.Codec is not IIndexedCodec codec)
                    continue;

                var palette = codec.Palette;
                var color = source.Pixels[i];
                var state = ImportPixelState.Unchanged;
                byte index;

                if (options.MapTransparentToIndexZero && color.A <= options.AlphaThreshold)
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
        }

        var report = new ImportReport(source.Width, source.Height, states,
            substitutions.Select(kv => new ColorMatchEntry(new ColorRgba32(kv.Key.Item1), kv.Key.Item2, kv.Value.Index, kv.Value.Distance, kv.Value.Count, kv.Value.First))
                .OrderByDescending(x => x.Distance).ThenByDescending(x => x.Count).ToList(),
            unmatched.Select(kv => new UnmatchedColorEntry(new ColorRgba32(kv.Key.Item1), kv.Key.Item2, kv.Value.Count, kv.Value.First, kv.Value.NearestIndex, kv.Value.NearestDistance))
                .OrderByDescending(x => x.Count).ToList());

        return new ImageImportPreview(arranger, source, report, current, result);
    }

    private static ImageImportPreview PrepareDirect(Arranger arranger, DecodedImage source)
    {
        var current = new DirectImage(arranger);
        var result = new DirectImage(arranger);
        var states = new ImportPixelState[source.Pixels.Length];

        for (int y = 0, i = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++, i++)
            {
                if (arranger.GetElementAtPixel(x, y)?.Codec is not IDirectCodec)
                    continue;

                var color = source.Pixels[i];
                result.Image[i] = color;

                if (color.Color != current.Image[i].Color)
                    states[i] = ImportPixelState.Changed;
            }
        }

        var report = new ImportReport(source.Width, source.Height, states, [], []);
        return new ImageImportPreview(arranger, source, report, current, result);
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
