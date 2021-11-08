using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project;

public sealed class ResourceFolderNode : ResourceNode<ResourceFolderModel>
{
    public ResourceFolderNode(string nodeName, ResourceFolder resource) : base(nodeName, resource)
    {
    }
}
