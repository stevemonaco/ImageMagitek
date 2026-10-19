namespace ImageMagitek.Project.Serialization;

public class CompressedDataModel : ResourceModel
{
    public override required string Name { get; init; }
    public required string ParentKey { get; init; }
    public required long FileOffset { get; init; }
    public required string Compression { get; init; }

    public override bool ResourceEquals(ResourceModel? resourceModel)
    {
        if (resourceModel is not CompressedDataModel model)
            return false;

        return model.Name == Name && model.ParentKey == ParentKey && model.FileOffset == FileOffset && model.Compression == Compression;
    }
}
