using System.Diagnostics.CodeAnalysis;
using ImageMagitek;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public partial class CompressedDataNodeViewModel : ResourceNodeViewModel
{
    public string Description
    {
        get
        {
            var source = (CompressedDataSource)Node.Item;
            return $"{source.CompressionName} at 0x{source.ParentOffset:X} of '{source.Parent.Name}' (read-only)";
        }
    }

    [SetsRequiredMembers]
    public CompressedDataNodeViewModel(ResourceNode node, ResourceNodeViewModel? parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;
    }

    internal override void RefreshDescription() => OnPropertyChanged(nameof(Description));
}
