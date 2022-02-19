using TileShop.AvaloniaUI.ViewModels;

namespace TileShop.Shared.Input;

public class IndexedPixelEditorStateDriver : PixelEditorStateDriver<IndexedPixelEditorViewModel, byte>
{
    public IndexedPixelEditorStateDriver(IndexedPixelEditorViewModel viewModel) : base(viewModel)
    {
    }
}
