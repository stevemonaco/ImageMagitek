using ImageMagitek.Colors;
using TileShop.AvaloniaUI.ViewModels;

namespace TileShop.Shared.Input;

public class DirectPixelEditorStateDriver : PixelEditorStateDriver<DirectPixelEditorViewModel, ColorRgba32>
{
    public DirectPixelEditorStateDriver(DirectPixelEditorViewModel viewModel) : base(viewModel)
    {
    }
}
