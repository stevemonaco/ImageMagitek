using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public class ArrangerNode : ResourceNode<ScatteredArrangerModel>
    {
        public ArrangerNode(string nodeName, ScatteredArranger resource) : base(nodeName, resource)
        {
        }
    }
}
