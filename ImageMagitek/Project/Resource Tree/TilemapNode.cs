using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project;

public sealed class TilemapNode : ResourceNode<TilemapModel>
{
    public TilemapNode(string nodeName, TilemapArranger resource) : base(nodeName, resource)
    {
    }
}
