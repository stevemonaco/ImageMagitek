using System.Collections.Generic;

namespace ImageMagitek.Project;
public sealed class ResourceFolder : IProjectResource
{
    public ResourceFolder(string name)
    {
        Name = name;
    }

    public string Name { get; set; }

    public bool CanContainChildResources => true;

    public bool ShouldBeSerialized { get; set; } = true;

    public IEnumerable<ResourceEdge> Dependencies => [];
}
