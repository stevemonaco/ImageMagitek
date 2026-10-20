using ImageMagitek.Metatiles;
using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project;

public sealed class MetatileMapNode : ResourceNode<MetatileMapModel>
{
    public MetatileMapNode(string nodeName, MetatileMap resource) : base(nodeName, resource)
    {
    }
}
