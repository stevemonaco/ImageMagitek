using System.Drawing;
using ImageMagitek.Colors;
using ImageMagitek.Image.Import;

namespace TileShop.UI.ViewModels;

public enum ImportColorEntryKind { Substituted, Unmatched }

/// <summary>
/// One row of the import report: a source color and the palette entry it maps to (or the nearest one when it cannot)
/// </summary>
public sealed class ImportColorEntryViewModel
{
    public ImportColorEntryKind Kind { get; }
    public ColorRgba32 Source { get; }
    public Avalonia.Media.Color SourceColor { get; }
    public Avalonia.Media.Color TargetColor { get; }
    public byte TargetIndex { get; }
    public double Distance { get; }
    public int Count { get; }
    public Point FirstLocation { get; }
    public string PaletteName { get; }

    public bool IsUnmatched => Kind == ImportColorEntryKind.Unmatched;
    public string SourceHex => ToHex(Source);
    public string TargetDescription => IsUnmatched ? $"nearest {TargetIndex}" : $"→ {TargetIndex}";
    public string CountDescription => $"×{Count:N0}";
    public string DistanceDescription => $"Δ {Distance:0.0}";
    public string LocationDescription => $"({FirstLocation.X}, {FirstLocation.Y})";

    public ImportColorEntryViewModel(ColorMatchEntry entry)
        : this(ImportColorEntryKind.Substituted, entry.Source, entry.Matched, entry.Index, entry.Distance, entry.Count, entry.FirstLocation, entry.Palette.Name)
    {
    }

    public ImportColorEntryViewModel(UnmatchedColorEntry entry)
        : this(ImportColorEntryKind.Unmatched, entry.Source, entry.Nearest, entry.NearestIndex, entry.NearestDistance, entry.Count, entry.FirstLocation, entry.Palette.Name)
    {
    }

    private ImportColorEntryViewModel(ImportColorEntryKind kind, ColorRgba32 source, ColorRgba32 target, byte targetIndex,
        double distance, int count, Point firstLocation, string paletteName)
    {
        Kind = kind;
        Source = source;
        SourceColor = ToMediaColor(source);
        TargetColor = ToMediaColor(target);
        TargetIndex = targetIndex;
        Distance = distance;
        Count = count;
        FirstLocation = firstLocation;
        PaletteName = paletteName;
    }

    private static Avalonia.Media.Color ToMediaColor(ColorRgba32 color) => Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B);

    private static string ToHex(ColorRgba32 color) =>
        color.A == 255 ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
}
