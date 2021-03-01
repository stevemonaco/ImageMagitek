using Monaco.PathTree;
using Monaco.PathTree.Abstractions;

namespace ImageMagitek.Project.Serialization
{
    public class ResourceModelTree : PathTreeBase<ResourceModelNode, ResourceModel, EmptyMetadata>
    {
        public ResourceModelTree(ResourceModelNode root) : base(root)
        {
        }
    }
}
