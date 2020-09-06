using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.WPF.ViewModels
{
    public class ArrangerNodeViewModel : TreeNodeViewModel
    {
        public override int SortPriority => 2;

        public ArrangerNodeViewModel(ResourceNode node, TreeNodeViewModel parent)
        {
            Node = node;
            Name = node.Name;
            Type = GetType();
            ParentModel = parent;
        }
    }
}
