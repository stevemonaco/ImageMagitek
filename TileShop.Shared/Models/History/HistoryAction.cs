using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.Shared.Models;

public abstract class HistoryAction : ObservableObject
{
    public abstract string Name { get; }
}
