using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.UI.Windowing;

namespace TileShop.UI.ViewModels;

public partial class NameResourceViewModel : DialogViewModel<string?>
{
    [ObservableProperty] private string? _resourceName;

    public NameResourceViewModel()
    {
        Title = "Name Resource";
        AcceptName = "✓";
        CancelName = "x";
    }

    protected override void Accept()
    {
        _requestResult = ResourceName;
        OnPropertyChanged(nameof(RequestResult));
    }
}
