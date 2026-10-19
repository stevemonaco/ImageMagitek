namespace ImageMagitek.Plugins;

/// <summary>
/// Describes a compression format to the host. The host validates it when the plugin is registered.
/// </summary>
public sealed class CompressionInfo
{
    /// <summary>
    /// Unique name that projects use to reference the format.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Short description shown when choosing a format, such as the games or systems that use it.
    /// </summary>
    public string Description { get; init; } = "";
}
