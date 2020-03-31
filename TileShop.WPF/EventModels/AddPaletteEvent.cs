using TileShop.WPF.ViewModels;

namespace TileShop.WPF.EventModels
{
    public class AddPaletteEvent
    {
        public TreeNodeViewModel Parent { get; set; }

        public AddPaletteEvent() { }

        public AddPaletteEvent(TreeNodeViewModel parent)
        {
            Parent = parent;
        }
    }
}
