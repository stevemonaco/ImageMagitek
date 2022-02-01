using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.AvaloniaUI.Models;

public partial class ColorRemapHistoryAction : HistoryAction
{
    public override string Name => "ColorRemap";

    [ObservableProperty] private ObservableCollection<RemappableColorModel> _initialColors;
    [ObservableProperty] private ObservableCollection<RemappableColorModel> _finalColors;

    public ColorRemapHistoryAction(IList<RemappableColorModel> initialColors, IList<RemappableColorModel> finalColors)
    {
        _initialColors = new(initialColors);
        _finalColors = new(finalColors);
    }
}
