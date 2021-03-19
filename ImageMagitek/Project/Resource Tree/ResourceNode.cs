using ImageMagitek.Project.Serialization;
using Monaco.PathTree.Abstractions;

namespace ImageMagitek.Project
{
    public class ResourceNode : PathNodeBase<ResourceNode, IProjectResource>
    {
        public string DiskLocation { get; set; }
        public ResourceModel Model { get; set; }

        public ResourceNode(string nodeName, IProjectResource resource) :
            base(nodeName, resource)
        {
        }
    }

    #pragma warning disable CS0108
    public class ResourceNode<TModel> : ResourceNode
        where TModel : ResourceModel
    {
        public ResourceNode(string nodeName, IProjectResource resource) : base(nodeName, resource)
        {
        }

        public TModel Model
        {
            get => (TModel) base.Model;
            set => base.Model = value;
        }
    }
}
