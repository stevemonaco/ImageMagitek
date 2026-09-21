using System;
using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Colors;

namespace ImageMagitek.Image.Import;

[Flags]
public enum ImportPixelState : byte
{
    Unchanged = 0,

    /// <summary>The imported pixel differs from the arranger's current pixel</summary>
    Changed = 1,

    /// <summary>The source color was not in the palette and the nearest entry was used</summary>
    Substituted = 2,

    /// <summary>The source color could not be matched, so the current pixel is kept</summary>
    Unmatched = 4
}

/// <summary>
/// A source color that was replaced by the nearest palette entry
/// </summary>
/// <param name="Count">Number of pixels with this source color in this palette</param>
/// <param name="FirstLocation">Pixel coordinate of the first occurrence</param>
public sealed record ColorMatchEntry(ColorRgba32 Source, Palette Palette, byte Index, double Distance, int Count, Point FirstLocation)
{
    public ColorRgba32 Matched => Palette[Index];
}

/// <summary>
/// A source color that no palette entry was acceptable for; the nearest entry is provided as a hint
/// </summary>
public sealed record UnmatchedColorEntry(ColorRgba32 Source, Palette Palette, int Count, Point FirstLocation, byte NearestIndex, double NearestDistance)
{
    public ColorRgba32 Nearest => Palette[NearestIndex];
}

/// <summary>
/// Describes what an import will do to an arranger, per pixel and per source color
/// </summary>
public sealed class ImportReport
{
    public int Width { get; }
    public int Height { get; }
    public int PixelCount => Width * Height;

    public int ChangedPixelCount { get; }
    public int SubstitutedPixelCount { get; }
    public int UnmatchedPixelCount { get; }

    /// <summary>Farthest substitutions first</summary>
    public IReadOnlyList<ColorMatchEntry> Substitutions { get; }

    /// <summary>Most frequent unmatched colors first</summary>
    public IReadOnlyList<UnmatchedColorEntry> Unmatched { get; }

    /// <summary>Per-pixel state in row-major order</summary>
    public ImportPixelState[] PixelStates { get; }

    public bool CanCommit => UnmatchedPixelCount == 0;

    public ImportReport(int width, int height, ImportPixelState[] pixelStates,
        IReadOnlyList<ColorMatchEntry> substitutions, IReadOnlyList<UnmatchedColorEntry> unmatched)
    {
        Width = width;
        Height = height;
        PixelStates = pixelStates;
        Substitutions = substitutions;
        Unmatched = unmatched;

        foreach (var state in pixelStates)
        {
            if (state.HasFlag(ImportPixelState.Changed))
                ChangedPixelCount++;
            if (state.HasFlag(ImportPixelState.Substituted))
                SubstitutedPixelCount++;
            if (state.HasFlag(ImportPixelState.Unmatched))
                UnmatchedPixelCount++;
        }
    }

    /// <summary>
    /// One-line description suitable for a status bar or console
    /// </summary>
    public string ToSummary()
    {
        var parts = new List<string> { $"{ChangedPixelCount:N0} of {PixelCount:N0} pixels change" };

        if (Substitutions.Count > 0)
            parts.Add($"{Substitutions.Count} {Pluralize(Substitutions.Count, "color")} substituted ({SubstitutedPixelCount:N0} pixels)");

        if (Unmatched.Count > 0)
            parts.Add($"{Unmatched.Count} {Pluralize(Unmatched.Count, "color")} unmatched ({UnmatchedPixelCount:N0} pixels)");

        return string.Join(" · ", parts);
    }

    private static string Pluralize(int count, string noun) => count == 1 ? noun : noun + "s";
}
