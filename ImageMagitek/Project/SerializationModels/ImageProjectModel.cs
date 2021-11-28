namespace ImageMagitek.Project.Serialization;

public class ImageProjectModel : ResourceModel
{
    public string Root { get; set; }
    public decimal Version { get; set; }

    public override bool ResourceEquals(ResourceModel resourceModel)
    {
        if (resourceModel is not ImageProjectModel model)
            return false;

        return model.Root == Root && model.Name == Name;
    }
}
