using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace TileShop.AvaloniaUI.ViewModels;

public class DockableToolViewModel : Tool
{
    public ObservableObject ToolViewModel { get; }

    public DockableToolViewModel(ObservableObject tool)
    {
        ToolViewModel = tool;
    }
}
