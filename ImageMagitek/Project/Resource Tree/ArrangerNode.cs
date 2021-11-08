using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project;

public sealed class ArrangerNode : ResourceNode<ScatteredArrangerModel>
{
    public ArrangerNode(string nodeName, ScatteredArranger resource) : base(nodeName, resource)
    {
    }
}
