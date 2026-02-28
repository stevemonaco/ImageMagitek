using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek.Colors;

namespace TileShop.UI.ViewModels;

public abstract partial class EditableColorBaseViewModel : ObservableRecipient
{
    public abstract bool CanSaveColor { get; }
    public required IColor WorkingColor { get; set; }

    [ObservableProperty] private Color _color;
    [ObservableProperty] private int _index;
    [ObservableProperty] private bool _isReadOnly;

    /// <summary>
    /// Command to save/assign the edited color. Set by the host (PaletteEditor, flyout, etc.)
    /// to decouple the color editor from its container.
    /// </summary>
    public IRelayCommand? SaveColorCommand { get; set; }

    public bool CanSave => !IsReadOnly && CanSaveColor;
}
