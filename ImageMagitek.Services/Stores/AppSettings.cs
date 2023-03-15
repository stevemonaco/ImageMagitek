using System.Collections.Generic;
using ImageMagitek.Colors;

namespace ImageMagitek.Services;

public sealed class AppSettings
{
    public required IDictionary<string, string> ExtensionCodecAssociations { get; init; }
    public required IList<string> GlobalPalettes { get; init; }
    public required string NesPalette { get; init; }
    public bool EnableArrangerSymmetryTools { get; set; }
    public ColorRgba32 GridLineColor { get; set; }
    public ColorRgba32 PrimaryGridBackgroundColor { get; set; }
    public ColorRgba32 SecondaryGridBackgroundColor { get; set; }
}
