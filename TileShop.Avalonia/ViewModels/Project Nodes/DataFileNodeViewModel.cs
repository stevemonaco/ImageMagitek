using ImageMagitek.Project;

namespace TileShop.AvaloniaUI.ViewModels;

public class DataFileNodeViewModel : ResourceNodeViewModel
{
    public override int SortPriority => 2;

    public DataFileNodeViewModel(ResourceNode node, ResourceNodeViewModel parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }
}
