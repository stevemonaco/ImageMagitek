using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileShop.AvaloniaUI.ViewExtenders;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class RenameNodeViewModel : DialogViewModel<string?>
{
    private ResourceNodeViewModel _nodeModel;

    [ObservableProperty] private string? _name;

    public RenameNodeViewModel(ResourceNodeViewModel nodeModel)
    {        
        _nodeModel = nodeModel;
        Name = nodeModel.Name;
        Title = $"Rename {nodeModel.Name}";
    }
}
