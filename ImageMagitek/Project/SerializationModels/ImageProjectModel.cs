﻿namespace ImageMagitek.Project.Serialization;

public class ImageProjectModel : ResourceModel
{
    public override required string Name { get; init; }
    public required string Root { get; init; }
    public decimal Version { get; set; }

    public override bool ResourceEquals(ResourceModel? resourceModel)
    {
        if (resourceModel is not ImageProjectModel model)
            return false;

        return model.Root == Root && model.Name == Name;
    }
}
