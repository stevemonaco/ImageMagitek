using Stylet;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreePaletteViewModel : Screen
    {
        public IPathTreeNode<IProjectResource> Node { get; set; }

        public string Name => Node.Name;

        public ProjectTreePaletteViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
        }
    }
}
