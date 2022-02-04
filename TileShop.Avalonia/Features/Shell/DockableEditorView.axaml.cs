using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TileShop.AvaloniaUI.Views;
public partial class DockableEditorView : UserControl
{
    public DockableEditorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
