using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.UI.Models;

/// <summary>
/// A palette color and the palette color its pixels will be remapped to
/// </summary>
public partial class RemappableColorModel : ObservableObject
{
    public byte Index { get; }
    public Color Color { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRemapped))]
    private byte _remappedIndex;

    [ObservableProperty] private Color _remappedColor;

    public bool IsRemapped => RemappedIndex != Index;

    public RemappableColorModel(Color color, byte index)
    {
        Color = color;
        Index = index;
        _remappedColor = color;
        _remappedIndex = index;
    }

    public void RemapTo(RemappableColorModel target)
    {
        RemappedIndex = target.Index;
        RemappedColor = target.Color;
    }

    public void Reset() => RemapTo(this);
}
