using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.Windowing;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class RenameNodeViewModel : DialogViewModel<string?>
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

    protected override void Accept()
    {
        _requestResult = Name;
        OnPropertyChanged(nameof(RequestResult));
    }
}
