using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek.Colors;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

/// <summary>
/// Edits a color chosen from a fixed table of system colors, such as the NES palette
/// </summary>
public partial class TableColorViewModel : EditableColorBaseViewModel
{
    private const int _columns = 16;

    public ObservableCollection<PaletteSwatchModel> AvailableColors { get; } = [];

    public int Columns => _columns;
    public IReadOnlyList<string> ColumnHeaders { get; }
    public IReadOnlyList<string> RowHeaders { get; }

    public override bool HasAlpha => false;

    public TableColorViewModel(ITableColor foreignColor, int index, IColorFactory colorFactory, ColorModel colorModel)
        : base(foreignColor, index, colorFactory, colorModel)
    {
        for (int i = 0; i <= foreignColor.ColorMax; i++)
        {
            var color = ToMediaColor(_colorFactory.CreateColor(_colorModel, (uint)i));
            AvailableColors.Add(new PaletteSwatchModel(i, color) { IsSelected = i == foreignColor.Color });
        }

        ColumnHeaders = Enumerable.Range(0, _columns).Select(x => x.ToString("X")).ToList();
        RowHeaders = Enumerable.Range(0, (AvailableColors.Count + _columns - 1) / _columns).Select(x => (x * _columns).ToString("X2")).ToList();
    }

    [RelayCommand]
    private void SelectTableColor(PaletteSwatchModel swatch)
    {
        ApplyWorkingColor(_colorFactory.CreateColor(_colorModel, (uint)swatch.Index));
    }

    protected override void ApplyWorkingColor(IColor color)
    {
        base.ApplyWorkingColor(color);

        foreach (var swatch in AvailableColors)
            swatch.IsSelected = swatch.Index == WorkingColor.Color;
    }
}
