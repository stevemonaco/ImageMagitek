using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek.Colors;
using TileShop.Shared.Interactions;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

/// <summary>
/// ViewModel responsible for remapping Palette colors of an indexed image
/// </summary>
public partial class ColorRemapViewModel : RequestViewModel<ColorRemapViewModel>
{
    public ObservableCollection<RemappableColorModel> Colors { get; } = [];

    /// <summary>
    /// Tells the user which part of the image the remap will be applied to
    /// </summary>
    public string ScopeDescription { get; init; } = "";

    public int Columns { get; }
    public double CellSize { get; }

    /// <summary>
    /// Index labels along the top of the grid; a cell's index is its row header plus its column header
    /// </summary>
    public IReadOnlyList<string> ColumnHeaders { get; }
    public IReadOnlyList<string> RowHeaders { get; }
    public bool HasMultipleRows => RowHeaders.Count > 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetAllCommand))]
    private bool _hasChanges;

    public ColorRemapViewModel(Palette palette, IColorFactory colorFactory) : this(palette, palette.Entries, colorFactory)
    {
    }

    /// <param name="paletteEntries">Number of colors to remap starting with the 0-index</param>
    public ColorRemapViewModel(Palette palette, int paletteEntries, IColorFactory colorFactory)
    {
        for (int i = 0; i < paletteEntries; i++)
        {
            var nativeColor = colorFactory.ToNative(palette[i]);
            var color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
            var model = new RemappableColorModel(color, (byte)i);
            model.PropertyChanged += OnColorPropertyChanged;
            Colors.Add(model);
        }

        Columns = Math.Min(paletteEntries, 16);
        CellSize = paletteEntries <= 8 ? 48 : 30;
        ColumnHeaders = Enumerable.Range(0, Columns).Select(x => x.ToString()).ToList();
        RowHeaders = Enumerable.Range(0, (paletteEntries + Columns - 1) / Columns).Select(x => (x * Columns).ToString()).ToList();

        Title = "Color Remapper";
        AcceptName = "Remap";
    }

    /// <summary>
    /// Creates the lookup table of new palette index per original palette index
    /// </summary>
    public byte[] CreateRemap() => Colors.Select(x => x.RemappedIndex).ToArray();

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void ResetAll()
    {
        foreach (var color in Colors)
            color.Reset();
    }

    private void OnColorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RemappableColorModel.IsRemapped))
            HasChanges = Colors.Any(x => x.IsRemapped);
    }

    public override ColorRemapViewModel? ProduceResult() => this;
}
