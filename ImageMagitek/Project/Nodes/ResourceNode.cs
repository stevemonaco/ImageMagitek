using Monaco.PathTree;
using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Project
{
    public class ResourceNode : PathTreeNode<IProjectResource>
    {
        public IProjectResource Resource { get; set; }

        public new ResourceNode Parent { get; set; }

        protected ResourceNode(string name, IProjectResource resource) : base(name, resource)
        {
            Resource = resource;
        }

        public new void AddChild(string name, IProjectResource value)
        {
            base.AttachChild(new ResourceNode(name, value));
        }

        public new IEnumerable<ResourceNode> Children { get => _children.Values.Cast<ResourceNode>(); }

        public new ResourceNode DetachChild(string name) => (ResourceNode)base.DetachChild(name);

        public bool TryGetChild(string name, out ResourceNode node)
        {
            if (base.TryGetChild(name, out var treeNode))
            {
                node = (ResourceNode)treeNode;
                return true;
            }
            else
            {
                node = null;
                return false;
            }
        }
    }

    public class ResourceNode<T> : ResourceNode
        where T : IProjectResource
    {
        public new T Resource { get; set; }

        public ResourceNode(string name, T resource) : base(name, resource) { }
    }
}
