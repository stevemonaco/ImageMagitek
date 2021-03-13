using ImageMagitek.Colors;
using ImageMagitek.Project.Serialization;

namespace ImageMagitek.Project
{
    public class PaletteNode : ResourceNode
    {
        public PaletteModel Model { get; set; }

        public PaletteNode(string nodeName, Palette resource) : base(nodeName, resource)
        {
        }
    }
}
