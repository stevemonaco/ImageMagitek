using System.Collections.Generic;

namespace ImageMagitek.Services;

public class AppSettings
{
    public required IDictionary<string, string> ExtensionCodecAssociations { get; init; }
    public required IList<string> GlobalPalettes { get; init; }
    public required string NesPalette { get; init; }
    public bool EnableArrangerSymmetryTools { get; set; }
}
