using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Controls;
using TileShop.AvaloniaUI.ViewExtenders.Dock;

namespace TileShop.AvaloniaUI.ViewModels;

public class DockableToolViewModel : Tool
{
    public ObservableObject ToolViewModel { get; }

    public DockableToolViewModel(ObservableObject tool)
    {
        ToolViewModel = tool;
    }
}
