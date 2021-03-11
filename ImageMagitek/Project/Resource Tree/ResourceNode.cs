using ImageMagitek.Project.Serialization;
using Monaco.PathTree.Abstractions;

namespace ImageMagitek.Project
{
    public class ResourceNode : PathNodeBase<ResourceNode, IProjectResource>
    {
        public string FileLocation { get; set; }

        public ResourceNode(string nodeName, IProjectResource resource) :
            base(nodeName, resource)
        {
        }
    }
}
