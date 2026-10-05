using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public class FolderNodeViewModel : ResourceNodeViewModel
{
    public override int SortPriority => 1;

    [SetsRequiredMembers]
    public FolderNodeViewModel(ResourceNode node, ResourceNodeViewModel parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }
}
