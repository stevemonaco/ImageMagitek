using Monaco.PathTree.Abstractions;

namespace ImageMagitek.Project.Serialization
{
    public class ResourceModelNode : PathNodeBase<ResourceModelNode, ResourceModel>
    {
        public ResourceModelNode(string nodeName, ResourceModel item) : base(nodeName, item)
        {
        }
    }
}
