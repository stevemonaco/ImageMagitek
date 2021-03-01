using Monaco.PathTree.Abstractions;
using Monaco.PathTree;

namespace ImageMagitek.Project.Serialization
{
    public class ResourceModelNode : PathNodeBase<ResourceModelNode, ResourceModel, EmptyMetadata>
    {
        public ResourceModelNode(string rootNodeName, ResourceModel item, EmptyMetadata metadata = default) : base(rootNodeName, item, metadata)
        {
        }

        protected override ResourceModelNode CreateNode(string nodeName, ResourceModel item, EmptyMetadata metadata = default)
        {
            return new ResourceModelNode(nodeName, item, metadata);
        }
    }
}
