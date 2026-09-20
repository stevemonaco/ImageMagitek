using System.Collections.Generic;

namespace ImageMagitek.Services;

/// <summary>
/// Application configuration shipped alongside the executable as appsettings.json.
/// Read once at startup; users may edit the file to override the defaults.
/// </summary>
public sealed record AppSettings(
    IDictionary<string, string> ExtensionCodecAssociations,
    IList<string> GlobalPalettes,
    string NesPalette
);
