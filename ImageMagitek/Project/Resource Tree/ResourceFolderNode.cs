using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public class ResourceFolderNode : ResourceNode
    {
        public ResourceFolderModel Model { get; set; }

        public ResourceFolderNode(string nodeName, IProjectResource resource) : base(nodeName, resource)
        {
        }
    }
}
