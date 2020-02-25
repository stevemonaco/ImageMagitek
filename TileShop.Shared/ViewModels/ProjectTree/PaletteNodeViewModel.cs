using Stylet;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.Shared.ViewModels
{
    public class PaletteNodeViewModel : TreeNodeViewModel
    {
        public PaletteNodeViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
            Name = node.Name;
            Type = GetType();
        }
    }
}
