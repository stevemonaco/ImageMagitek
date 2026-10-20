using System;
using System.Drawing;
using Avalonia.Controls;
using Avalonia.Input;
using TileShop.UI.Controls;

namespace TileShop.UI.Features.Graphics;

/// <summary>
/// A brush palette shown on a <see cref="PaletteCanvasInput"/>: its cell size, pixel width and pointer handlers in cell coordinates
/// </summary>
public interface ICellPalette
{
    Size CellPixelSize { get; }
    int PixelWidth { get; }
    void PointerDown(int cellX, int cellY);
    void PointerMove(int cellX, int cellY);
    void PointerUp();
    void PointerExited();
}

/// <summary>
/// Routes a palette canvas's pointer input to its <see cref="ICellPalette"/>: left-drag picks cells, middle-drag pans,
/// the wheel scrolls or Ctrl+wheel zooms, and the first nonzero size fits the palette's width
/// </summary>
public sealed class PaletteCanvasInput
{
    private readonly InfiniteCanvas _canvas;
    private readonly Func<ICellPalette?> _palette;
    private bool _isFitPending = true;

    public PaletteCanvasInput(InfiniteCanvas canvas, Func<ICellPalette?> palette)
    {
        _canvas = canvas;
        _palette = palette;

        canvas.PointerPressed += OnPointerPressed;
        canvas.PointerReleased += OnPointerReleased;
        canvas.PointerMoved += OnPointerMoved;
        canvas.PointerExited += (_, _) => _palette()?.PointerExited();
        canvas.PointerWheelChanged += OnPointerWheelChanged;
        canvas.SizeChanged += OnSizeChanged;
    }

    /// <summary>
    /// Fits the palette's width at a whole-number zoom so cells stay crisp, then shows its top rows
    /// </summary>
    public void FitWidth()
    {
        if (_palette() is not { } palette || _canvas.Bounds.Width <= 0)
            return;

        var zoom = Math.Floor(_canvas.Bounds.Width / palette.PixelWidth);
        _canvas.Zoom = Math.Clamp(zoom, _canvas.MinZoom, _canvas.MaxZoom);
        _canvas.AlignTopLeft();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isFitPending && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            _isFitPending = false;
            FitWidth();
        }
    }

    private (int X, int Y) CellAt(ICellPalette palette, PointerEventArgs e)
    {
        var size = palette.CellPixelSize;
        var local = _canvas.ScreenToLocalPoint(e.GetPosition(_canvas));
        return ((int)Math.Floor(local.X / size.Width), (int)Math.Floor(local.Y / size.Height));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_palette() is not { } palette)
            return;

        var point = e.GetCurrentPoint(_canvas);

        if (point.Properties.IsLeftButtonPressed)
        {
            var (x, y) = CellAt(palette, e);
            palette.PointerDown(x, y);
            e.Pointer.Capture(_canvas);
            e.Handled = true;
        }
        else if (point.Properties.IsMiddleButtonPressed)
        {
            _canvas.StartPan(point.Position);
            e.Pointer.Capture(_canvas);
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _canvas.EndPan();
        _palette()?.PointerUp();
        e.Pointer.Capture(null);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_palette() is not { } palette)
            return;

        var (x, y) = CellAt(palette, e);
        palette.PointerMove(x, y);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0)
            return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Delta.Y > 0)
                _canvas.ZoomIn();
            else
                _canvas.ZoomOut();
        }
        else
        {
            _canvas.OffsetY = Math.Max(0, _canvas.OffsetY - e.Delta.Y * 48);
            _canvas.Invalidate();
        }

        e.Handled = true;
    }
}
