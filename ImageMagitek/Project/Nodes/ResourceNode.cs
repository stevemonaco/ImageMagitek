using Monaco.PathTree;
using System.Collections.Generic;
using System.Linq;

namespace ImageMagitek.Project
{
    public class ResourceNode : PathTreeNode<IProjectResource, ResourceMetadata>
    {
        public IProjectResource Resource { get; set; }

        public new ResourceNode Parent { get => (ResourceNode) base.Parent; set => base.Parent = value; }

        protected ResourceNode(string name, IProjectResource resource) : base(name, resource)
        {
            Resource = resource;
        }

        public void AddChild(string name, IProjectResource value)
        {
            base.AddChild(name, value);
        }

        public new IEnumerable<ResourceNode> ChildNodes { get => _children?.Values.Cast<ResourceNode>() ?? Enumerable.Empty<ResourceNode>(); }

        public new ResourceNode DetachChildNode(string name) => (ResourceNode)base.DetachChildNode(name);

        public bool TryGetChildNode(string name, out ResourceNode node)
        {
            if (base.TryGetChildNode(name, out var treeNode))
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
        public new T Resource { get => (T) base.Resource; set => base.Resource = value; }

        public ResourceNode(string name, T resource) : base(name, resource) { }
    }
}
