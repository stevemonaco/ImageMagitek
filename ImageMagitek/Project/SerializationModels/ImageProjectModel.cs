using System;

namespace ImageMagitek.Project.Serialization;

public class ImageProjectModel : ResourceModel
{
    public override required string Name { get; init; }
    public required string Root { get; init; }

    /// <summary>
    /// The project format version the file holds or will be written with
    /// </summary>
    public required Version Version { get; init; }

    public override bool ResourceEquals(ResourceModel? resourceModel)
    {
        if (resourceModel is not ImageProjectModel model)
            return false;

        return model.Root == Root && model.Name == Name && model.Version == Version;
    }
}
