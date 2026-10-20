using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;
using TileShop.Shared.Models;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

/// <summary>
/// Views a metatile map composited from its layers, with a layer visibility list, block or tile gridlines and composite export
/// </summary>
public partial class MetatileMapEditorViewModel : ResourceEditorBaseViewModel
{
    private readonly Func<Task> _export;

    public MetatileMap Map { get; }
    public ObservableCollection<LayerVisibilityViewModel> Layers { get; }
    public GridSettingsViewModel GridSettings { get; }

    [ObservableProperty] private ArrangerSkiaBitmap _bitmap;
    [ObservableProperty] private bool _showTileGrid;

    public Action? OnImageModified { get; set; }
    public Action? OnFitToViewport { get; set; }
    public Action? OnResetZoom { get; set; }

    /// <param name="export">Runs the Export As... flow for the map</param>
    public MetatileMapEditorViewModel(MetatileMap map, GridPreferences gridPreferences, Func<Task> export) : base(map)
    {
        Map = map;
        _export = export;
        DisplayName = map.Name;

        ShowAllLayers();

        Layers = new(Enumerable.Range(0, map.LayerCount)
            .Select(i => new LayerVisibilityViewModel(LayerName(i, map.LayerCount), i, OnLayerVisibilityChanged)));

        GridSettings = GridSettingsViewModel.CreateDefault(map.Layers[0], gridPreferences);
        ApplyGridSpacing();
        GridSettings.PropertyChanged += OnGridSettingsPropertyChanged;

        _bitmap = new ArrangerSkiaBitmap(map.Composite());
    }

    private static string LayerName(int index, int count) => (count, index) switch
    {
        (2, 0) => "Bottom",
        (2, 1) => "Top",
        _ => $"Layer {index + 1}"
    };

    /// <summary>
    /// Makes every layer of the map visible, so exports made without an open editor include all layers
    /// </summary>
    public void ShowAllLayers()
    {
        for (int i = 0; i < Map.LayerCount; i++)
            Map.SetLayerVisible(i, true);
    }

    private void OnLayerVisibilityChanged(int index, bool isVisible)
    {
        Map.SetLayerVisible(index, isVisible);
        Redraw();
    }

    private void OnGridSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GridSettingsViewModel.ShowGridlines) or nameof(GridSettingsViewModel.LineColor) or nameof(GridSettingsViewModel.Gridlines))
            OnImageModified?.Invoke();
    }

    partial void OnShowTileGridChanged(bool value) => ApplyGridSpacing();

    private void ApplyGridSpacing()
    {
        var spacing = ShowTileGrid ? Map.TileSize : Map.BlockPixelSize;
        GridSettings.WidthSpacing = spacing.Width;
        GridSettings.HeightSpacing = spacing.Height;
        GridSettings.AdjustGridlines(Map.Layers[0]);
    }

    /// <summary>
    /// Composites the map again with the current layer visibility and data
    /// </summary>
    public void Redraw()
    {
        var previous = Bitmap;
        Bitmap = new ArrangerSkiaBitmap(Map.Composite());
        previous.Dispose();
        OnImageModified?.Invoke();
    }

    /// <summary>
    /// Whether the map draws from <paramref name="resource"/>, directly or through compressed data
    /// </summary>
    public bool ReadsFrom(IProjectResource resource) => resource switch
    {
        Palette palette => Map.PaletteSlots.Contains(palette),
        DataSource source => Map.DataSources
            .Any(x => ReferenceEquals(x, source) || x is CompressedDataSource compressed && ReferenceEquals(compressed.Parent, source)),
        _ => false
    };

    [RelayCommand]
    private Task ExportAs() => _export();

    [RelayCommand]
    private void FitToViewport() => OnFitToViewport?.Invoke();

    [RelayCommand]
    private void ResetZoom() => OnResetZoom?.Invoke();

    public override void Undo() { }
    public override void Redo() { }
    public override Task SaveChangesAsync() => Task.CompletedTask;
    public override void DiscardChanges() { }
}
