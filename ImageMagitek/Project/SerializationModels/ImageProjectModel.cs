using System;

namespace ImageMagitek.Project.Serialization;

public sealed record ImageProjectModel : ResourceModel
{
    public required string Root { get; init; }

    /// <summary>
    /// The project format version the file holds or will be written with
    /// </summary>
    public required Version Version { get; init; }
}
