using TileShop.WPF.ViewModels;

namespace TileShop.WPF.EventModels
{
    public class AddScatteredArrangerEvent
    {
        public ResourceNodeViewModel Parent { get; set; }

        public AddScatteredArrangerEvent() { }

        public AddScatteredArrangerEvent(ResourceNodeViewModel parent)
        {
            Parent = parent;
        }
    }
}
