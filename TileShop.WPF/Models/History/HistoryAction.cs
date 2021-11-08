using Stylet;

namespace TileShop.WPF.Models;

public abstract class HistoryAction : PropertyChangedBase
{
    public abstract string Name { get; }
}
