namespace ImageMagitek.Project.Serialization;

public sealed record CompressedDataModel : ResourceModel
{
    public required string ParentKey { get; init; }
    public required long FileOffset { get; init; }
    public required string Compression { get; init; }
}
