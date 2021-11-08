using System.Collections.Generic;

namespace ImageMagitek.Services;

public class AppSettings
{
    public IDictionary<string, string> ExtensionCodecAssociations { get; set; }
    public IList<string> GlobalPalettes { get; set; }
    public string NesPalette { get; set; }
    public bool EnableArrangerSymmetryTools { get; set; }
}
