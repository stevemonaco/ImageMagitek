using Monaco.PathTree.Abstractions;

namespace ImageMagitek.Project
{
    public class ResourceNode : PathNodeBase<ResourceNode, IProjectResource, ResourceMetadata>
    {
        public ResourceNode(string nodeName, IProjectResource resource, ResourceMetadata metadata = default) :
            base(nodeName, resource, metadata)
        {
        }

        protected override ResourceNode CreateNode(string nodeName, IProjectResource item, ResourceMetadata metadata = null)
        {
            return new ResourceNode(nodeName, item, metadata);
        }
    }
}
