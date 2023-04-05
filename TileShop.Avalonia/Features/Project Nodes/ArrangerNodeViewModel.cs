using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public class ArrangerNodeViewModel : ResourceNodeViewModel
{
    public override int SortPriority => 2;

    [SetsRequiredMembers]
    public ArrangerNodeViewModel(ResourceNode node, ResourceNodeViewModel parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }
}
