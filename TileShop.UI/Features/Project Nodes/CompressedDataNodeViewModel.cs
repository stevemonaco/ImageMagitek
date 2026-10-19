using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public partial class CompressedDataNodeViewModel : ResourceNodeViewModel
{
    [ObservableProperty] private string _description = "";

    [SetsRequiredMembers]
    public CompressedDataNodeViewModel(ResourceNode node, ResourceNodeViewModel? parent)
    {
        Node = node;
        Name = node.Name;
        ParentModel = parent;

        var source = (CompressedDataSource)node.Item;
        Description = $"{source.CompressionName} at 0x{source.ParentOffset:X} of '{source.Parent.Name}' (read-only)";
    }
}
