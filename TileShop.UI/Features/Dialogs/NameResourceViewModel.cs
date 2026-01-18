using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.UI.Controls;

namespace TileShop.UI.ViewModels;

public partial class NameResourceViewModel : RequestBaseViewModel<string?>
{
    [ObservableProperty] private string? _resourceName;

    public NameResourceViewModel()
    {
        Title = "Name Resource";
        AcceptName = "✓";
        CancelName = "x";
    }

    public override string? ProduceResult() => ResourceName;
}
