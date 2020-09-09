using ImageMagitek.Project;
using Monaco.PathTree;

namespace TileShop.WPF.ViewModels
{
    public class DataFileNodeViewModel : ResourceNodeViewModel
    {
        public override int SortPriority => 2;

        public DataFileNodeViewModel(ResourceNode node, ResourceNodeViewModel parent)
        {
            Node = node;
            Name = node.Name;
            Type = GetType();
            ParentModel = parent;
        }
    }
}
