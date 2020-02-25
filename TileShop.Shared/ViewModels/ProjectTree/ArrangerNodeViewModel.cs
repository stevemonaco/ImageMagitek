using Stylet;
using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.Shared.ViewModels
{
    public class ArrangerNodeViewModel : TreeNodeViewModel
    {
        public ArrangerNodeViewModel(IPathTreeNode<IProjectResource> node)
        {
            Node = node;
            Name = node.Name;
            Type = GetType();
        }
    }
}
