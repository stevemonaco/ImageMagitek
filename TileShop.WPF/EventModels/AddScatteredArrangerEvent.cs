using TileShop.WPF.ViewModels;

namespace TileShop.WPF.EventModels
{
    public class AddScatteredArrangerEvent
    {
        public TreeNodeViewModel Parent { get; set; }

        public AddScatteredArrangerEvent() { }

        public AddScatteredArrangerEvent(TreeNodeViewModel parent)
        {
            Parent = parent;
        }
    }
}
