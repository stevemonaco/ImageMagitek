using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.UI.Controls;

namespace TileShop.UI.ViewModels;

public partial class RenameNodeViewModel : RequestBaseViewModel<string?>
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

    protected override void Accept()
    {
        RequestResult = Name;
        OnPropertyChanged(nameof(RequestResult));
    }
}
