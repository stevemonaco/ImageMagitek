namespace ImageMagitek.Project.Serialization;

public sealed record DataFileModel : ResourceModel
{
    public required string Location { get; init; }
}
