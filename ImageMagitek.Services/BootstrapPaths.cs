using System.IO;

namespace ImageMagitek.Services;

/// <summary>
/// Locations of the shipped resources that bootstrapping reads
/// </summary>
public sealed record BootstrapPaths(
    string SettingsFileName,
    string PalettesPath,
    string CodecsPath,
    string PluginsPath,
    string LayoutsPath,
    string ResourceSchemaFileName,
    string CodecSchemaFileName)
{
    /// <summary>
    /// Resolves the default resource names against <paramref name="directory"/>, normally the application directory
    /// </summary>
    public static BootstrapPaths FromDirectory(string directory) => new(
        Path.Combine(directory, "appsettings.json"),
        Path.Combine(directory, "_palettes"),
        Path.Combine(directory, "_codecs"),
        Path.Combine(directory, "_plugins"),
        Path.Combine(directory, "_layouts"),
        Path.Combine(directory, "_schemas", "ResourceSchema.xsd"),
        Path.Combine(directory, "_schemas", "CodecSchema.xsd"));
}
