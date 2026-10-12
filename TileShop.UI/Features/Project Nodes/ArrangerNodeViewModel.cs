using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public class ArrangerNodeViewModel : ResourceNodeViewModel
{
    [SetsRequiredMembers]
    public ArrangerNodeViewModel(ResourceNode node, ResourceNodeViewModel parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }
}
