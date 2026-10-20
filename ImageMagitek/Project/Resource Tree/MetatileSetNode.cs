using ImageMagitek.Metatiles;
using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project;

public sealed class MetatileSetNode : ResourceNode<MetatileSetModel>
{
    public MetatileSetNode(string nodeName, MetatileSet resource) : base(nodeName, resource)
    {
    }
}
