using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using ImageMagitek.Tilemaps;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Features.Graphics.Tools;

namespace TileShop.UI.ViewModels;

public partial class GraphicsEditorViewModel
{
    private TilemapPlotter? _tilePlotter;
    private readonly CellStroke _tileStroke = new();

    [ObservableProperty] private TilePaletteViewModel? _tilePalette;
    [ObservableProperty] private TileTool _selectedTileTool = TileTool.Pencil;

    public TileTool DisplayedTileTool => _toolRouter.IsOverrideEngaged && EditMode == GraphicsEditMode.Tiles
        ? TileTool.Picker : SelectedTileTool;

    public bool IsCellStrokeActive => _tileStroke.Name is not null;

    partial void OnSelectedTileToolChanged(TileTool oldValue, TileTool newValue)
    {
        OnPropertyChanged(nameof(DisplayedTileTool));

        if (_tileTools.TryGetValue(oldValue, out var outgoing) && outgoing.Deactivate(this) is { } historyAction)
            AddHistoryAction(historyAction);
    }

    [RelayCommand]
    public void ChangeTileTool(TileTool tool) => SelectedTileTool = tool;

    [RelayCommand]
    private void EditTileSet()
    {
        if (_tilemap is not null)
            Messenger.Send(new EditTileSetMessage(_tilemap));
    }

    /// <summary>
    /// Points the plotter at the current working grid, which undo, save and discard replace
    /// </summary>
    private void RebuildTilePlotting()
    {
        if (!CanPlotTiles || _tilemap is null || WorkingGrid is not { } grid)
            return;

        _tilePlotter = new TilemapPlotter(_tilemap, grid);

        if (TilePalette is null)
            TilePalette = new TilePaletteViewModel(_tilemap, GridSettings);
        else
            TilePalette.Refresh();
    }

    public Point CellAt(int pixelX, int pixelY) =>
        new(pixelX / WorkingArranger.ElementPixelSize.Width, pixelY / WorkingArranger.ElementPixelSize.Height);

    /// <summary>
    /// The pixel bounds of <paramref name="cells"/> within the map, or null when none of them is in it
    /// </summary>
    public Rectangle? CellsToPixels(Rectangle cells) =>
        CellRect.ToPixels(cells, WorkingArranger.ElementPixelSize, WorkingArranger.ArrangerElementSize);

    /// <summary>
    /// Where the brush would land for a pointer over <paramref name="cell"/>, snapped to the stroke's grid during a stroke
    /// </summary>
    internal Rectangle? BrushFootprint(Point cell)
    {
        if (TilePalette?.Brush is not { } brush)
            return null;

        if (_tileStroke.Origin is { } origin)
            cell = TilemapPlotter.SnapToStroke(brush, origin, cell);

        return CellsToPixels(new Rectangle(cell.X, cell.Y, brush.Width, brush.Height));
    }

    public void BeginCellStroke(string name)
    {
        EndCellStroke();
        _tileStroke.Begin(name);
    }

    /// <summary>
    /// Ends the stroke, recording one history step when it changed a cell
    /// </summary>
    public void EndCellStroke()
    {
        if (_tileStroke.End() is { } name)
            AddHistoryAction(new PlotTilesHistoryAction(name));
    }

    /// <summary>
    /// Stamps the brush at every cell from the previous stroke cell to <paramref name="cell"/>
    /// </summary>
    public void PlotStroke(Point cell)
    {
        if (_tilePlotter is not { } plotter || !TryGetBrush(plotter, out var brush))
            return;

        var origin = _tileStroke.Origin ??= cell;
        var changed = false;
        foreach (var point in _tileStroke.Advance(cell))
        {
            var snapped = TilemapPlotter.SnapToStroke(brush, origin, point);
            changed |= plotter.Stamp(brush, snapped.X, snapped.Y);
        }

        CompleteTileEdit(changed);
    }

    internal void EraseStroke(Point cell)
    {
        if (_tilePlotter is not { } plotter)
            return;

        var changed = false;
        foreach (var point in _tileStroke.Advance(cell))
            changed |= plotter.Erase(new Rectangle(point.X, point.Y, 1, 1));

        CompleteTileEdit(changed);
    }

    internal void FillTiles(Rectangle cells)
    {
        if (_tilePlotter is not { } plotter || !TryGetBrush(plotter, out var brush))
            return;

        BeginCellStroke("Fill Tiles");
        CompleteTileEdit(plotter.Fill(brush, cells));
        EndCellStroke();
    }

    /// <summary>
    /// Makes the cells under <paramref name="cells"/> the brush
    /// </summary>
    internal void PickTiles(Rectangle cells)
    {
        if (_tilePlotter?.Pick(cells) is { } brush)
            TilePalette?.SetBrush(brush);
        else
            Messenger.Send(new NotifyStatusMessage("No tile of the tile set to pick there"));
    }

    private bool TryGetBrush(TilemapPlotter plotter, out TileBrush brush)
    {
        brush = TilePalette?.Brush!;
        var error = brush is null ? "Pick a tile from the tile palette" : plotter.FindBrushError(brush);
        if (error is null)
            return true;

        if (_tileStroke.ShouldReport())
            Messenger.Send(new NotifyStatusMessage(error));

        return false;
    }

    private void CompleteTileEdit(bool changed)
    {
        if (!changed)
            return;

        _tileStroke.Changed = true;
        IsModified = true;
        InvalidateEditor(InvalidationLevel.PixelData);
    }
}
