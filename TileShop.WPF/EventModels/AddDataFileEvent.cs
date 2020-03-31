using TileShop.WPF.ViewModels;

namespace TileShop.WPF.EventModels
{
    public class AddDataFileEvent
    {
        public TreeNodeViewModel Parent { get; set; }

        public AddDataFileEvent() { }

        public AddDataFileEvent(TreeNodeViewModel parent)
        {
            Parent = parent;
        }
    }
}
