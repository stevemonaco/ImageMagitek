using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.Windowing;

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
