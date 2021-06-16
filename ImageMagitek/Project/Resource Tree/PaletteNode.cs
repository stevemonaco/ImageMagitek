using ImageMagitek.Colors;
using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public sealed class PaletteNode : ResourceNode<PaletteModel>
    {
        public PaletteNode(string nodeName, Palette resource) : base(nodeName, resource)
        {
        }
    }
}
