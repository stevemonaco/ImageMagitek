using Avalonia.Controls;
using Avalonia.Input;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;
public partial class IndexedPixelEditorView : PixelEditorView<IndexedPixelEditorViewModel>
{
    public IndexedPixelEditorView()
    {
        InitializeComponent();
        _penPreview = penPreview;
        _image = image;
    }

    public void OnPaletteEntryPressed(object sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is PaletteEntry entry && ViewModel is not null)
        {
            var properties = e.GetCurrentPoint(this).Properties;

            if (properties.IsLeftButtonPressed)
                ViewModel?.SetPrimaryColor(entry.Index);
            else if (properties.IsRightButtonPressed)
                ViewModel?.SetSecondaryColor(entry.Index);

            e.Handled = true;
        }
    }
}
