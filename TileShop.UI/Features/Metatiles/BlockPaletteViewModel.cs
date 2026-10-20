using System;
using System.Drawing;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Metatiles;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Features.Graphics.Tools;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

/// <summary>
/// The Blocks mode side panel: the block palette a brush is drag-selected from
/// </summary>
public sealed partial class BlockPaletteViewModel : ObservableObject, ICellPalette
{
    private readonly MetatileMap _map;
    private Point? _dragAnchor;

    public BlockPalette Palette { get; }
    public ArrangerSkiaBitmap Bitmap { get; private set; }
    public GridSettingsViewModel GridSettings { get; }
    public Size BlockPixelSize => _map.BlockPixelSize;
    Size ICellPalette.CellPixelSize => BlockPixelSize;
    int ICellPalette.PixelWidth => Bitmap.Width;

    /// <summary>
    /// Called when the palette's image or selection needs repainting
    /// </summary>
    public Action? OnInvalidated { get; set; }

    [ObservableProperty] private BlockBrush? _brush;
    [ObservableProperty] private Rectangle? _selectionRect;
    [ObservableProperty] private string? _statusText;

    public int MinBlockNumber => _map.Sets.Min(x => x.Base);
    public int MaxBlockNumber => _map.Sets.Max(x => x.Base + x.Set.Count - 1);

    /// <summary>
    /// The brush's block when it holds one block, otherwise null; setting a number some set holds makes a one-block brush of it
    /// </summary>
    public int? BrushBlockNumber
    {
        get => Brush is { Width: 1, Height: 1 } brush ? brush.GetCell(0, 0) : null;
        set
        {
            if (value is not int number || BrushBlockNumber == number)
                return;

            if (_map.FindSetFor(number) is null)
            {
                StatusText = $"Block {number} is in no metatile set";
                OnPropertyChanged();
                return;
            }

            StatusText = null;
            SetBrush(BlockBrush.Single(number));
        }
    }

    public string BrushDescription => Brush switch
    {
        null => "No brush: pick a block from the block palette",
        { Width: 1, Height: 1 } brush when brush.GetCell(0, 0) is { } number => $"Brush: block {number}",
        { } brush => $"Brush: {brush.Width}x{brush.Height} blocks"
    };

    public BlockPaletteViewModel(MetatileMap map, GridSettingsViewModel gridSettings)
    {
        _map = map;
        GridSettings = gridSettings;
        Palette = BlockPalette.Generate(map);
        Bitmap = new ArrangerSkiaBitmap(Palette.Render());
    }

    partial void OnBrushChanged(BlockBrush? value)
    {
        OnPropertyChanged(nameof(BrushBlockNumber));
        OnPropertyChanged(nameof(BrushDescription));
    }

    /// <summary>
    /// Makes <paramref name="brush"/> the brush, such as one picked from the map
    /// </summary>
    public void SetBrush(BlockBrush brush)
    {
        Brush = brush;
        SelectionRect = brush is { Width: 1, Height: 1 } && brush.GetCell(0, 0) is { } number && Palette.FindCell(number) is { } cell
            ? new Rectangle(cell.X, cell.Y, 1, 1) : null;
        OnInvalidated?.Invoke();
    }

    /// <summary>
    /// Draws the blocks again, after a layer was shown or hidden or the metatiles or tiles changed
    /// </summary>
    public void Refresh()
    {
        var previous = Bitmap;
        Bitmap = new ArrangerSkiaBitmap(Palette.Render());
        previous.Dispose();
        OnInvalidated?.Invoke();
    }

    public void PointerDown(int cellX, int cellY)
    {
        if (Palette.GetBlockNumber(cellX, cellY) is null)
            return;

        _dragAnchor = new Point(cellX, cellY);
        SelectCells(new Rectangle(cellX, cellY, 1, 1));
    }

    public void PointerMove(int cellX, int cellY)
    {
        if (_dragAnchor is { } anchor)
        {
            SelectCells(CellRect.Between(anchor, new Point(cellX, cellY)));
            return;
        }

        StatusText = Palette.GetBlockNumber(cellX, cellY) is { } number ? DescribeBlock(number) : null;
    }

    public void PointerUp() => _dragAnchor = null;

    public void PointerExited()
    {
        if (_dragAnchor is null)
            StatusText = null;
    }

    private void SelectCells(Rectangle cells)
    {
        if (Palette.CreateBrush(cells) is not { } brush)
            return;

        Brush = brush;
        SelectionRect = Rectangle.Intersect(cells, new Rectangle(0, 0, Palette.Width, Palette.Height));
        OnInvalidated?.Invoke();
    }

    private string DescribeBlock(int number) =>
        _map.FindSetFor(number) is { } set ? $"Block {number}  ·  {set.Set.Name}" : $"Block {number}";
}
