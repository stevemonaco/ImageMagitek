using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.AvaloniaUI.Models;

public abstract class HistoryAction : ObservableObject
{
    public abstract string Name { get; }
}
