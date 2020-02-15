using Stylet;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeDataFileViewModel : ProjectTreeNodeViewModel
    {
        public ProjectTreeDataFileViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
            Name = node.Name;
        } 
    }
}
