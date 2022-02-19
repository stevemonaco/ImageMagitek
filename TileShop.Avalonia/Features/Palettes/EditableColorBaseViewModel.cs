using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Colors;

namespace TileShop.AvaloniaUI.ViewModels;

public abstract partial class EditableColorBaseViewModel : ObservableObject
{
    public abstract bool CanSaveColor { get; }
    public IColor WorkingColor { get; set; }

    [ObservableProperty] private Color _color;
    [ObservableProperty] private int _index;
}
