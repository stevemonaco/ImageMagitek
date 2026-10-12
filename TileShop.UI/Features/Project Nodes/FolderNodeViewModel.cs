using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public class FolderNodeViewModel : ResourceNodeViewModel
{
    [SetsRequiredMembers]
    public FolderNodeViewModel(ResourceNode node, ResourceNodeViewModel parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }
}
