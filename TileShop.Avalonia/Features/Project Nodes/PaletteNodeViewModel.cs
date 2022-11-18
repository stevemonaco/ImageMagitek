using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Project;

namespace TileShop.AvaloniaUI.ViewModels;

public class PaletteNodeViewModel : ResourceNodeViewModel
{
    public override int SortPriority => 2;

    [SetsRequiredMembers]
    public PaletteNodeViewModel(ResourceNode node, ResourceNodeViewModel parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }
}
