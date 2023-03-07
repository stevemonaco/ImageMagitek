namespace ImageMagitek.Project.Serialization;

public class ResourceFolderModel : ResourceModel
{
    public override required string Name { get; init; }

    public override bool ResourceEquals(ResourceModel? resourceModel)
    {
        if (resourceModel is not ResourceFolderModel model)
            return false;

        return model.Name == Name;
    }
}
