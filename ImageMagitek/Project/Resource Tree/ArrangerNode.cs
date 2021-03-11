using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public class ArrangerNode : ResourceNode
    {
        public ScatteredArrangerModel Model { get; set; }

        public ArrangerNode(string nodeName, IProjectResource resource) : base(nodeName, resource)
        {
        }
    }
}
