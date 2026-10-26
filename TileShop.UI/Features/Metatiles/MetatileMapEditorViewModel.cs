using System;
using System.Collections.Generic;
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
using ImageMagitek.Scenes;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Features.Graphics.Tools;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

public enum MetatileEditMode { View, Blocks }

/// <summary>
/// Views a metatile map composited from its layers, with a layer visibility list, block or tile gridlines, a block-bits overlay and composite export,
/// and plots whole blocks from a block palette in Blocks mode
/// </summary>
public partial class MetatileMapEditorViewModel : ResourceEditorBaseViewModel, ICellPlotState
{
    private readonly Func<Func<LayerImage>, Task> _export;
    private readonly IInteractionService _interactions;

    public MetatileMap Map { get; }
    public ObservableCollection<LayerVisibilityViewModel> Layers { get; }
    public GridSettingsViewModel GridSettings { get; }

    [ObservableProperty] private ArrangerSkiaBitmap _bitmap;
    [ObservableProperty] private bool _showTileGrid;
    [ObservableProperty] private bool _showBlockBits;
    [ObservableProperty] private BlockPaletteViewModel? _blockPalette;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBlocksMode))]
    private MetatileEditMode _editMode = MetatileEditMode.View;

    public bool IsBlocksMode => EditMode == MetatileEditMode.Blocks;

    /// <summary>
    /// Why Blocks mode is unavailable, or null when the block map can be written
    /// </summary>
    public string? BlocksDisabledReason { get; }
    public bool CanPlotBlocks => BlocksDisabledReason is null;

    public Action? OnImageModified { get; set; }
    public Action? OnFitToViewport { get; set; }
    public Action? OnResetZoom { get; set; }

    public override IReadOnlyList<Hotkey> Hotkeys => field ??=
    [
        new("Ctrl+S", SaveChangesCommand),
        new("Ctrl+Z", UndoCommand),
        new("Ctrl+Y", RedoCommand),
        new("Ctrl+W", FitToViewportCommand),
        new("Ctrl+R", ResetZoomCommand),
    ];

    public override EditCommands EditCommands => field ??= new(UndoCommand, RedoCommand, EditCommands.Disabled, EditCommands.Disabled,
        EditCommands.Disabled, EditCommands.Disabled, EditCommands.Disabled);

    /// <param name="export">Runs the Export As... flow for the map, writing the composite it is given</param>
    public MetatileMapEditorViewModel(MetatileMap map, GridPreferences gridPreferences, IInteractionService interactions, Func<Func<LayerImage>, Task> export) : base(map)
    {
        Map = map;
        _interactions = interactions;
        _export = export;
        DisplayName = map.Name;

        ShowAllLayers();

        Layers = new(Enumerable.Range(0, map.LayerCount)
            .Select(i => new LayerVisibilityViewModel(LayerName(i, map.LayerCount), i, OnLayerVisibilityChanged)));

        GridSettings = GridSettingsViewModel.CreateDefault(map.Layers[0], gridPreferences);
        ApplyGridSpacing();
        GridSettings.PropertyChanged += OnGridSettingsPropertyChanged;

        BlocksDisabledReason = map.BlockMapReadOnlyReason is { } reason ? $"The block map is read-only because it {reason}" : null;

        _grid = map.CreateBlockGrid();
        _baseGrid = _grid.Clone();
        _plotter = new MetatileMapPlotter(map, _grid);

        _toolRouter = new ToolInputRouter<MetatileMapEditorViewModel>(SelectedTool, TemporaryTool, TemporaryTool, AlternativeToolKeys);
        _bitmap = new ArrangerSkiaBitmap(map.Composite(_grid));
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

    partial void OnShowBlockBitsChanged(bool value) => OnImageModified?.Invoke();

    private void ApplyGridSpacing()
    {
        var spacing = ShowTileGrid ? Map.TileSize : Map.BlockPixelSize;
        GridSettings.WidthSpacing = spacing.Width;
        GridSettings.HeightSpacing = spacing.Height;
        GridSettings.AdjustGridlines(Map.Layers[0]);
    }

    /// <summary>
    /// Composites the block grid again with the current layer visibility, metatiles and tiles, and redraws the block palette
    /// </summary>
    public void Redraw()
    {
        BlockPalette?.Refresh();
        RedrawMap();
    }

    private void RedrawMap()
    {
        var previous = Bitmap;
        Bitmap = new ArrangerSkiaBitmap(Map.Composite(_grid));
        previous.Dispose();
        OnImageModified?.Invoke();
    }

    /// <summary>
    /// Follows a write to a source the map reads: an unmodified editor re-reads its block grid, a modified one keeps it; both redraw
    /// </summary>
    public void OnSourceChanged()
    {
        if (!IsModified)
            ResetGrid(Map.CreateBlockGrid());

        Redraw();
    }

    /// <inheritdoc/>
    public override bool DependsOn(IReadOnlySet<IProjectResource> resources) => base.DependsOn(resources) || Map.Sets.Any(x => resources.Contains(x.Set));

    public override void OnClosing() => ShowAllLayers();

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
    private void ChangeEditMode(MetatileEditMode mode)
    {
        if (mode == EditMode || mode == MetatileEditMode.Blocks && !CanPlotBlocks)
            return;

        if (_toolRouter.Reset(this) is { } historyAction)
            AddHistoryAction(historyAction);

        EditMode = mode;
        if (mode == MetatileEditMode.Blocks)
            BlockPalette ??= new BlockPaletteViewModel(Map, GridSettings);

        SetHoverTarget(null);
        NotifyDisplayedToolChanged();
    }

    [RelayCommand]
    private Task ExportAs() => _export(() => Map.Composite(_grid));

    [RelayCommand]
    private void FitToViewport() => OnFitToViewport?.Invoke();

    [RelayCommand]
    private void ResetZoom() => OnResetZoom?.Invoke();

    [RelayCommand]
    public override async Task SaveChangesAsync()
    {
        var result = Map.WriteBlocks(_grid);
        if (result.HasFailed)
        {
            await _interactions.AlertAsync("Save Error", result.AsError.Reason);
            return;
        }

        _baseGrid = _grid.Clone();
        ClearHistory();
        IsModified = false;
        Redraw();
    }

    public override void DiscardChanges()
    {
        ResetGrid(Map.CreateBlockGrid());
        IsModified = false;
        Redraw();
    }

    private void ResetGrid(BlockGrid grid)
    {
        SetGrid(grid);
        _baseGrid = grid.Clone();
        ClearHistory();
    }

    private void SetGrid(BlockGrid grid)
    {
        _grid = grid;
        _plotter = new MetatileMapPlotter(Map, grid);
    }
}
