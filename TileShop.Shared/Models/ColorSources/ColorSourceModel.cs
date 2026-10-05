using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.Shared.Models;

public abstract partial class ColorSourceModel : ObservableValidator
{
    /// <summary>
    /// True while the source holds a color selected in the palette editor
    /// </summary>
    [ObservableProperty] private bool _isHighlighted;
}