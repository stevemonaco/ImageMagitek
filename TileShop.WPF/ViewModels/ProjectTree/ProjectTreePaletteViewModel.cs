using Stylet;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreePaletteViewModel : ProjectTreeNodeViewModel
    {
        public ProjectTreePaletteViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
            Name = node.Name;
        }
    }
}
