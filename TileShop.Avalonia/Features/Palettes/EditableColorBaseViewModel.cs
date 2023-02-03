using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Colors;

namespace TileShop.AvaloniaUI.ViewModels;

public abstract partial class EditableColorBaseViewModel : ObservableRecipient
{
    public abstract bool CanSaveColor { get; }
    public required IColor WorkingColor { get; set; }

    [ObservableProperty] private Color _color;
    [ObservableProperty] private int _index;
}
