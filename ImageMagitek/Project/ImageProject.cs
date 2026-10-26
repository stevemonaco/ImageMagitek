using System.Collections.Generic;

namespace ImageMagitek.Project;

public sealed class ImageProject : IProjectResource
{
    public string Name { get; set; }
    public string Root { get; set; }

    public bool CanContainChildResources => true;

    public bool ShouldBeSerialized { get; set; } = true;

    public ImageProject() : this("") { }

    public ImageProject(string name)
    {
        Name = name;
        Root = "";
    }

    public void Rename(string name)
    {
        Name = name;
    }

    public IEnumerable<ResourceEdge> Dependencies => [];
}
