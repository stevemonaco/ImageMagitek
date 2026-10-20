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

namespace TileShop.UI.ViewModels;

public partial class GraphicsEditorViewModel
{
    private TilemapPlotter? _tilePlotter;
    private string? _tileStrokeName;
    private bool _tileStrokeChanged;
    private bool _tileStrokeFailureShown;
    private Point? _tileStrokeOrigin;
    private Point? _lastStrokeCell;

    [ObservableProperty] private TilePaletteViewModel? _tilePalette;
    [ObservableProperty] private TileTool _selectedTileTool = TileTool.Pencil;

    public TileTool DisplayedTileTool => _toolRouter.IsOverrideEngaged && EditMode == GraphicsEditMode.Tiles
        ? TileTool.Picker : SelectedTileTool;

    public bool IsTileStrokeActive => _tileStrokeName is not null;

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
        if (_projectArranger is TilemapArranger tilemap)
            Messenger.Send(new EditTileSetMessage(tilemap));
    }

    /// <summary>
    /// Points the plotter at the current working copy, which undo, save and discard replace
    /// </summary>
    private void RebuildTilePlotting()
    {
        if (!CanPlotTiles || WorkingArranger is not ScatteredArranger working)
            return;

        var tilemap = (TilemapArranger)_projectArranger;

        _tilePlotter = new TilemapPlotter(tilemap, working);

        if (TilePalette is null)
            TilePalette = new TilePaletteViewModel(tilemap, GridSettings);
        else
            TilePalette.Refresh();
    }

    internal Point CellAt(int pixelX, int pixelY) =>
        new(pixelX / WorkingArranger.ElementPixelSize.Width, pixelY / WorkingArranger.ElementPixelSize.Height);

    internal static Rectangle CellRectangle(Point a, Point b) =>
        Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X) + 1, Math.Max(a.Y, b.Y) + 1);

    /// <summary>
    /// The pixel bounds of <paramref name="cells"/> within the map, or null when none of them is in it
    /// </summary>
    internal Rectangle? CellsToPixels(Rectangle cells)
    {
        var size = WorkingArranger.ElementPixelSize;
        var clipped = Rectangle.Intersect(cells, new Rectangle(Point.Empty, WorkingArranger.ArrangerElementSize));
        if (clipped.Width <= 0 || clipped.Height <= 0)
            return null;

        return new Rectangle(clipped.X * size.Width, clipped.Y * size.Height, clipped.Width * size.Width, clipped.Height * size.Height);
    }

    /// <summary>
    /// Where the brush would land for a pointer over <paramref name="cell"/>, snapped to the stroke's grid during a stroke
    /// </summary>
    internal Rectangle? BrushFootprint(Point cell)
    {
        if (TilePalette?.Brush is not { } brush)
            return null;

        if (_tileStrokeOrigin is { } origin)
            cell = TilemapPlotter.SnapToStroke(brush, origin, cell);

        return CellsToPixels(new Rectangle(cell.X, cell.Y, brush.Width, brush.Height));
    }

    internal void BeginTileStroke(string name)
    {
        EndTileStroke();
        _tileStrokeName = name;
        _tileStrokeFailureShown = false;
    }

    /// <summary>
    /// Ends the stroke, recording one history step when it changed a cell
    /// </summary>
    internal void EndTileStroke()
    {
        if (_tileStrokeName is { } name && _tileStrokeChanged)
            AddHistoryAction(new PlotTilesHistoryAction(name));

        _tileStrokeName = null;
        _tileStrokeChanged = false;
        _tileStrokeOrigin = null;
        _lastStrokeCell = null;
    }

    /// <summary>
    /// Forgets the last stroke cell, so a pointer re-entering the map does not plot across the gap
    /// </summary>
    internal void BreakTileStroke() => _lastStrokeCell = null;

    /// <summary>
    /// Stamps the brush at every cell from the previous stroke cell to <paramref name="cell"/>
    /// </summary>
    internal void PlotStroke(Point cell)
    {
        if (_tilePlotter is not { } plotter || !TryGetBrush(plotter, out var brush))
            return;

        var origin = _tileStrokeOrigin ??= cell;
        var changed = false;
        foreach (var point in StrokeCells(cell))
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
        foreach (var point in StrokeCells(cell))
            changed |= plotter.Erase(new Rectangle(point.X, point.Y, 1, 1));

        CompleteTileEdit(changed);
    }

    internal void FillTiles(Rectangle cells)
    {
        if (_tilePlotter is not { } plotter || !TryGetBrush(plotter, out var brush))
            return;

        BeginTileStroke("Fill Tiles");
        CompleteTileEdit(plotter.Fill(brush, cells));
        EndTileStroke();
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

    private IEnumerable<Point> StrokeCells(Point cell)
    {
        var cells = _lastStrokeCell is { } last ? PixelLine.Points(last, cell).Skip(1).ToList() : [cell];
        _lastStrokeCell = cell;
        return cells;
    }

    private bool TryGetBrush(TilemapPlotter plotter, out TileBrush brush)
    {
        brush = TilePalette?.Brush!;
        var error = brush is null ? "Pick a tile from the tile palette" : plotter.FindBrushError(brush);
        if (error is null)
            return true;

        if (!_tileStrokeFailureShown)
        {
            _tileStrokeFailureShown = IsTileStrokeActive;
            Messenger.Send(new NotifyStatusMessage(error));
        }

        return false;
    }

    private void CompleteTileEdit(bool changed)
    {
        if (!changed)
            return;

        _tileStrokeChanged = true;
        IsModified = true;
        InvalidateEditor(InvalidationLevel.PixelData);
    }
}
