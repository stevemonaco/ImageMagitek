using Stylet;
using System.Collections.Generic;
using TileShop.Shared.Models;

namespace TileShop.WPF.Models;

public class ColorRemapHistoryAction : HistoryAction
{
    public override string Name => "ColorRemap";

    private BindableCollection<RemappableColorModel> _initialColors = new BindableCollection<RemappableColorModel>();
    public BindableCollection<RemappableColorModel> InitialColors
    {
        get => _initialColors;
        set => SetProperty(ref _initialColors, value);
    }

    private BindableCollection<RemappableColorModel> _finalColors = new BindableCollection<RemappableColorModel>();
    public BindableCollection<RemappableColorModel> FinalColors
    {
        get => _finalColors;
        set => SetProperty(ref _finalColors, value);
    }

    public ColorRemapHistoryAction(IList<RemappableColorModel> initialColors, IList<RemappableColorModel> finalColors)
    {
        _initialColors.AddRange(initialColors);
        _finalColors.AddRange(finalColors);
    }
}
