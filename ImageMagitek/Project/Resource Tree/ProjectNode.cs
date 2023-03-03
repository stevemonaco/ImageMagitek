using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project;

public sealed class ProjectNode : ResourceNode<ImageProjectModel>
{
    public string BaseDirectory { get; }

    public ProjectNode(string baseDirectory, string nodeName, ImageProject resource) : base(nodeName, resource)
    {
        BaseDirectory = baseDirectory;
    }
}
