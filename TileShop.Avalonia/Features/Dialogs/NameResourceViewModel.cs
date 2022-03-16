using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.ViewExtenders;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class NameResourceViewModel : DialogViewModel<string?>
{
    [ObservableProperty] private string? _resourceName;

    public NameResourceViewModel()
    {
        Title = "Name Resource";
    }
}
