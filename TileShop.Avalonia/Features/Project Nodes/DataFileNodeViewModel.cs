using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public class DataFileNodeViewModel : ResourceNodeViewModel
{
    public override int SortPriority => 2;

    [SetsRequiredMembers]
    public DataFileNodeViewModel(ResourceNode node, ResourceNodeViewModel parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }
}
