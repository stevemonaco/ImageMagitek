using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.UI.Models;

/// <summary>
/// One selectable cell of a swatch grid
/// </summary>
public partial class PaletteSwatchModel : ObservableObject
{
    public int Index { get; }

    [ObservableProperty] private Color _color;
    [ObservableProperty] private bool _isSelected;

    public PaletteSwatchModel(int index, Color color)
    {
        Index = index;
        _color = color;
    }
}
