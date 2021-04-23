using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public class ResourceFolderNode : ResourceNode<ResourceFolderModel>
    {
        public ResourceFolderNode(string nodeName, ResourceFolder resource) : base(nodeName, resource)
        {
        }
    }
}
