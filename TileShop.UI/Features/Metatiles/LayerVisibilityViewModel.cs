using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.UI.ViewModels;

/// <summary>
/// One metatile layer's check box in the metatile map editor's layer list
/// </summary>
public partial class LayerVisibilityViewModel : ObservableObject
{
    private readonly Action<int, bool> _visibilityChanged;

    public string Name { get; }
    public int Index { get; }

    [ObservableProperty] private bool _isVisible = true;

    public LayerVisibilityViewModel(string name, int index, Action<int, bool> visibilityChanged)
    {
        Name = name;
        Index = index;
        _visibilityChanged = visibilityChanged;
    }

    partial void OnIsVisibleChanged(bool value) => _visibilityChanged(Index, value);
}
