namespace ImageMagitek.Project;

/// <summary>
/// A resource whose contents can be unavailable because a data file it reads is missing or its data could not be read (LIB-PROJECT-TREE-036)
/// </summary>
public interface IUnavailableResource
{
    /// <summary>
    /// The first missing file the resource reads, directly or through compressed data, its elements or its palettes, or null when none is missing
    /// </summary>
    FileDataSource? FindMissingDataSource();

    /// <summary>
    /// Why the resource's contents cannot be read, such as a tilemap's map error, or null when they can
    /// </summary>
    string? UnavailableReason => null;
}
