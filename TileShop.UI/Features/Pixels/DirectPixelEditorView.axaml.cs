using TileShop.UI.ViewModels;

namespace TileShop.UI.Views;
public partial class DirectPixelEditorView : PixelEditorView<IndexedPixelEditorViewModel>
{
    public DirectPixelEditorView()
    {
        InitializeComponent();
        _penPreview = penPreview;
        _image = image;
    }
}
