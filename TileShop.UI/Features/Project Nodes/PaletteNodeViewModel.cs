using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public class PaletteNodeViewModel : ResourceNodeViewModel
{
    [SetsRequiredMembers]
    public PaletteNodeViewModel(ResourceNode node, ResourceNodeViewModel parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }
}
