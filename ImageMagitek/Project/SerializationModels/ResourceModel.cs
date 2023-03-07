using System.Collections.Generic;

namespace ImageMagitek.Project.Serialization;

public abstract class ResourceModel
{
    public abstract required string Name { get; init; }
    public ResourceModel? Parent { get; set; }
    internal Dictionary<string, ResourceModel> ChildResources { get; } = new Dictionary<string, ResourceModel>();

    public abstract bool ResourceEquals(ResourceModel? model);
}
