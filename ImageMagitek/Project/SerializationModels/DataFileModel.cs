using System;

namespace ImageMagitek.Project.Serialization;

public class DataFileModel : ResourceModel
{
    public string Location { get; set; }

    public override bool ResourceEquals(ResourceModel resourceModel)
    {
        if (resourceModel is not DataFileModel model)
            return false;

        return model.Location == Location && model.Name == Name;
    }
}
