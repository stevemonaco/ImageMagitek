using Stylet;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeArrangerViewModel : Screen
    {
        public IPathTreeNode<IProjectResource> Node { get; set; }

        public string Name => Node.Name;

        public ProjectTreeArrangerViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
        }
    }
}
