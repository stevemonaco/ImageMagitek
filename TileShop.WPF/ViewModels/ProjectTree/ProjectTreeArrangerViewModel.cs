using Stylet;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeArrangerViewModel : ProjectTreeNodeViewModel
    {
        public ProjectTreeArrangerViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
            Name = node.Name;
            Type = GetType();
        }
    }
}
