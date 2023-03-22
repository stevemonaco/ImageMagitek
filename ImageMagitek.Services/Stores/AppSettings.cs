using System.Collections.Generic;
using ImageMagitek.Colors;

namespace ImageMagitek.Services;

/// <summary>
/// Contains settings that the user may want to modify, preserve, and/or share
/// </summary>
public sealed record AppSettings(
    IDictionary<string, string> ExtensionCodecAssociations,
    IList<string> GlobalPalettes,
    string NesPalette,
    bool EnableArrangerSymmetryTools,
    ColorRgba32 GridLineColor,
    ColorRgba32 PrimaryGridBackgroundColor,
    ColorRgba32 SecondaryGridBackgroundColor
);