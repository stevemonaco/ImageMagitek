using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewModels;

public partial class NameResourceViewModel : RequestViewModel<string?>
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
