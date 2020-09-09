using TileShop.WPF.ViewModels;

namespace TileShop.WPF.EventModels
{
    public class AddPaletteEvent
    {
        public ResourceNodeViewModel Parent { get; set; }

        public AddPaletteEvent() { }

        public AddPaletteEvent(ResourceNodeViewModel parent)
        {
            Parent = parent;
        }
    }
}
