using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewModels;
public partial class RenameNodeViewModel : RequestViewModel<string?>
{
    private readonly ResourceNodeViewModel _nodeModel;

    [ObservableProperty] private string _name;

    public RenameNodeViewModel(ResourceNodeViewModel nodeModel)
    {        
        _nodeModel = nodeModel;
        _name = nodeModel.Name;
        Title = $"Rename {nodeModel.Name}";
        AcceptName = "✓";
        CancelName = "x";
    }

    public override string? ProduceResult() => Name;
}
