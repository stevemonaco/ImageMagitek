using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using ImageMagitek.Tilemaps;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics;

/// <summary>
/// A palette slot the tile brush can use, named for the combo
/// </summary>
public sealed record TileSlotOption(int Slot, string Name);

/// <summary>
/// The Tiles mode side panel: the tile palette a brush is drag-selected from, and the brush's palette slot and flips
/// </summary>
public sealed partial class TilePaletteViewModel : ObservableObject
{
    private readonly TilemapArranger _tilemap;
    private ArrangerImageAdapter _imageAdapter = null!;
    private Point? _dragAnchor;

    public TilePalette Palette { get; private set; } = null!;
    public ArrangerSkiaBitmap Bitmap { get; private set; } = null!;
    public GridSettingsViewModel GridSettings { get; }
    public IReadOnlyList<TileSlotOption> SlotOptions { get; }

    /// <summary>
    /// Called when the palette's image or selection needs repainting
    /// </summary>
    public Action? OnInvalidated { get; set; }

    /// <summary>
    /// Called when the palette is rebuilt with another size, so the view can refit it
    /// </summary>
    public Action? OnPaletteReplaced { get; set; }

    [ObservableProperty] private TileSlotOption _selectedSlot;
    [ObservableProperty] private TileBrush? _brush;
    [ObservableProperty] private bool _horizontalFlip;
    [ObservableProperty] private bool _verticalFlip;
    [ObservableProperty] private Rectangle? _selectionRect;
    [ObservableProperty] private string? _statusText;

    public int MinTileIndex => _tilemap.FirstTileIndex;
    public int MaxTileIndex => Math.Max(_tilemap.FirstTileIndex, _tilemap.FirstTileIndex + _tilemap.TileCount - 1);

    /// <summary>
    /// The brush's tile when it holds one tile, otherwise null; setting it makes a one-tile brush, so the brush can be chosen without the pointer
    /// </summary>
    public int? BrushTileIndex
    {
        get => Brush is { Width: 1, Height: 1 } brush && brush.GetCell(0, 0) is { } cell ? cell.TileIndex : null;
        set
        {
            if (value is not int index || BrushTileIndex == index)
                return;

            Brush = TileBrush.Single(index, SelectedSlot.Slot, HorizontalFlip, VerticalFlip);
            SelectionRect = FindPaletteCell(index) is { } cell ? new Rectangle(cell.X, cell.Y, 1, 1) : null;
            OnInvalidated?.Invoke();
        }
    }

    public string BrushDescription => Brush switch
    {
        null => "No brush: pick a tile from the tile palette",
        { Width: 1, Height: 1 } brush when brush.GetCell(0, 0) is { } cell => $"Brush: tile {cell.TileIndex}",
        { } brush => $"Brush: {brush.Width}x{brush.Height} tiles"
    };

    public TilePaletteViewModel(TilemapArranger tilemap, GridSettingsViewModel gridSettings)
    {
        _tilemap = tilemap;
        GridSettings = gridSettings;

        SlotOptions = Enumerable.Range(0, tilemap.PaletteSlots.Count)
            .Where(i => i == 0 || tilemap.PaletteSlots[i] is not null)
            .Select(i => new TileSlotOption(i, $"Slot {i}: {tilemap.PaletteSlots[i]?.Name ?? "default"}"))
            .ToList();
        _selectedSlot = SlotOptions.First(x => x.Slot == tilemap.MostUsedPaletteSlot);

        Rebuild();
    }

    partial void OnSelectedSlotChanged(TileSlotOption value)
    {
        Rebuild();

        if (Brush is not null)
            Brush = Brush.WithSlot(value.Slot);
    }

    partial void OnHorizontalFlipChanged(bool value) => ApplyFlips();
    partial void OnVerticalFlipChanged(bool value) => ApplyFlips();

    partial void OnBrushChanged(TileBrush? value)
    {
        OnPropertyChanged(nameof(BrushTileIndex));
        OnPropertyChanged(nameof(BrushDescription));
    }

    private void ApplyFlips()
    {
        if (Brush is not null)
            Brush = Brush.WithFlips(HorizontalFlip, VerticalFlip);
    }

    /// <summary>
    /// Makes <paramref name="brush"/> the brush, such as one picked from the map, showing its slot and flips in the controls
    /// </summary>
    public void SetBrush(TileBrush brush)
    {
        if (SlotOptions.FirstOrDefault(x => x.Slot == brush.Slot) is { } option)
            SelectedSlot = option;

        HorizontalFlip = brush.HorizontalFlip;
        VerticalFlip = brush.VerticalFlip;
        Brush = brush;
        SelectionRect = null;
        OnInvalidated?.Invoke();
    }

    /// <summary>
    /// Re-reads the tiles, after the tile set's pixels or palettes changed
    /// </summary>
    public void Refresh()
    {
        _imageAdapter.Render();
        Bitmap.Invalidate();
        OnInvalidated?.Invoke();
    }

    private void Rebuild()
    {
        var previousSize = Palette?.Arranger.ArrangerPixelSize;
        Palette = _tilemap.TilePaletteArranger is { } curated
            ? TilePalette.FromArranger(_tilemap, curated, SelectedSlot.Slot)
            : TilePalette.Generate(_tilemap, SelectedSlot.Slot);

        _imageAdapter = new ArrangerImageAdapter(Palette.Arranger);
        var previousBitmap = Bitmap;
        Bitmap = _imageAdapter.CreateSkiaBitmap();
        previousBitmap?.Dispose();

        StatusText = DefaultStatus;
        if (previousSize != Palette.Arranger.ArrangerPixelSize)
            OnPaletteReplaced?.Invoke();
        OnInvalidated?.Invoke();
    }

    private string? DefaultStatus => _tilemap.TileCount == 0 ? "The tile set has no tiles" : null;

    public void PointerDown(int cellX, int cellY)
    {
        if (Palette.GetTileIndex(cellX, cellY) is null)
        {
            StatusText = DescribeCell(cellX, cellY);
            return;
        }

        _dragAnchor = new Point(cellX, cellY);
        SelectCells(new Rectangle(cellX, cellY, 1, 1));
    }

    public void PointerMove(int cellX, int cellY)
    {
        if (_dragAnchor is { } anchor)
        {
            SelectCells(GraphicsEditorViewModel.CellRectangle(anchor, new Point(cellX, cellY)));
            return;
        }

        StatusText = DescribeCell(cellX, cellY);
    }

    public void PointerUp() => _dragAnchor = null;

    public void PointerExited()
    {
        if (_dragAnchor is null)
            StatusText = DefaultStatus;
    }

    private void SelectCells(Rectangle cells)
    {
        if (Palette.CreateBrush(cells, SelectedSlot.Slot) is not { } brush)
            return;

        Brush = brush.WithFlips(HorizontalFlip, VerticalFlip);
        SelectionRect = Rectangle.Intersect(cells, new Rectangle(0, 0, Palette.Width, Palette.Height));
        OnInvalidated?.Invoke();
    }

    private string? DescribeCell(int cellX, int cellY) =>
        Palette.GetRefusal(cellX, cellY) is { } refusal ? $"Element at ({cellX}, {cellY}) {refusal}" : DefaultStatus;

    private Point? FindPaletteCell(int tileIndex)
    {
        for (int y = 0; y < Palette.Height; y++)
        {
            for (int x = 0; x < Palette.Width; x++)
            {
                if (Palette.GetTileIndex(x, y) == tileIndex)
                    return new Point(x, y);
            }
        }

        return null;
    }
}
