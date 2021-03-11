using Monaco.PathTree.Abstractions;

namespace ImageMagitek.Project.Serialization
{
    public class ProjectModelTree : PathTreeBase<ResourceModelNode, ResourceModel>
    {
        public ProjectModelTree(ResourceModelNode root) : base(root)
        {
        }
    }
}
