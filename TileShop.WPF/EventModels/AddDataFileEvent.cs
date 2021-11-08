using TileShop.WPF.ViewModels;

namespace TileShop.WPF.EventModels;

public class AddDataFileEvent
{
    public ResourceNodeViewModel Parent { get; set; }

    public AddDataFileEvent() { }

    public AddDataFileEvent(ResourceNodeViewModel parent)
    {
        Parent = parent;
    }
}
