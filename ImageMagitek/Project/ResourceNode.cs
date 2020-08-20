using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public class ResourceNode : PathTreeNode<IProjectResource>
    {
        public ResourceNode(string name, IProjectResource value) : base(name, value)
        {

        }
    }
}
