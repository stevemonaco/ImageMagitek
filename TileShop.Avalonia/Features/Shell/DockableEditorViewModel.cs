using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.ViewExtenders.Dock;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class DockableEditorViewModel : Document
{
    [ObservableProperty] private ResourceEditorBaseViewModel _editor;

    public DockableEditorViewModel(ResourceEditorBaseViewModel editor)
    {
        _editor = editor;
    }
}
