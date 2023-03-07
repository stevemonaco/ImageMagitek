namespace ImageMagitek.Project.Serialization;

public class DataFileModel : ResourceModel
{
    public override required string Name { get; init; }
    public required string Location { get; init; }

    public override bool ResourceEquals(ResourceModel? resourceModel)
    {
        if (resourceModel is not DataFileModel model)
            return false;

        return model.Location == Location && model.Name == Name;
    }
}
