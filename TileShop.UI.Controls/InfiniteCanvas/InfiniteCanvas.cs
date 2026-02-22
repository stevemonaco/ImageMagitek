using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace TileShop.UI.Controls;

public enum RenderTrigger { Continuous, Invalidation }
public enum PointAlignment { TopLeft, Left, BottomLeft, Top, Center, Bottom, TopRight, Right, BottomRight }

/// <summary>
/// Supports panning, zooming, and rendering to an SkCanvas
/// </summary>
/// <remarks>
/// Code adapted from https://github.com/AvaloniaUI/Avalonia/discussions/12269#discussioncomment-6513790, SkiaSharp,
/// and PanAndZoom
/// </remarks>
public partial class InfiniteCanvas : Control
{
    /// <summary>
    /// Event to externally paint the Skia surface (using the <see cref="SKCanvas"/>).
    /// </summary>
    public event EventHandler<SKPaintSurfaceEventArgs>? PaintSurface;
    
    /// <summary>
    /// Event raised while RenderTrigger.Continuous is set when the animation state should be updated
    /// </summary>
    public event EventHandler<UpdateStateEventArgs>? UpdateState;

    protected bool _isDirty;
    protected bool _isLoaded;
    protected bool _isUpdating;
    
    protected Stopwatch _renderStopwatch = new();
    protected SKPaint? _frameTextFill;
    protected SKFont? _frameTextFont;
    protected TopLevel? _topLevel;

    protected readonly Vector _dpi = new(96, 96);
    protected WriteableBitmap? _bitmap;

    protected void PrepareFrame(TimeSpan dt)
    {
        OnUpdateState(new UpdateStateEventArgs(dt));

        Invalidate();
        
        if (RenderTrigger == RenderTrigger.Continuous)
            _topLevel!.RequestAnimationFrame(PrepareFrame);
    }

    public override void Render(DrawingContext context)
    {
        if (_bitmap is null)
            return;

        var rect = new Rect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
        context.DrawImage(_bitmap, rect, rect);
    }

    /// <summary>
    /// Invalidates the canvas causing the surface to be repainted.
    /// This will fire the <see cref="PaintSurface"/> event.
    /// </summary>
    public void Invalidate()
    {
        if (!_isDirty && _isLoaded)
        {
            _isDirty = true;
            Dispatcher.UIThread.Post(RepaintSurface, DispatcherPriority.Render);
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _isLoaded = true;
        
        if (RenderTrigger == RenderTrigger.Continuous)
        {
            _topLevel!.RequestAnimationFrame(PrepareFrame);
        }
        else if (RenderTrigger == RenderTrigger.Invalidation)
        {
            Invalidate();
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        _renderStopwatch.Stop();
        _frameTextFill?.Dispose();
        _frameTextFont?.Dispose();
        _bitmap?.Dispose();

        _frameTextFill = null;
        _frameTextFont = null;
        _bitmap = null;
    }

    /// <summary>
    /// Repaints the Skia surface and canvas.
    /// </summary>
    private void RepaintSurface()
    {
        _renderStopwatch.Restart();

        if (!IsVisible)
        {
            return;
        }

        int pixelWidth = (int)Bounds.Width;
        int pixelHeight = (int)Bounds.Height;

        if (pixelWidth == 0 || pixelHeight == 0)
            return;

        bool isNewBitmap = false;
        if (_bitmap is null || (int)_bitmap.Size.Width != pixelWidth || (int)_bitmap.Size.Height != pixelHeight)
        {
            _bitmap?.Dispose();

            _bitmap = new WriteableBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                _dpi,
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            isNewBitmap = true;
        }

        using (var framebuffer = _bitmap.Lock())
        {
            if (!isNewBitmap)
            {
                ClearBitmap(framebuffer, (int)_bitmap.Size.Height);
            }

            var info = new SKImageInfo(
                framebuffer.Size.Width,
                framebuffer.Size.Height,
                framebuffer.Format.ToSkColorType(),
                SKAlphaType.Premul);

            using var properties = new SKSurfaceProperties(SKPixelGeometry.RgbHorizontal);

            // When creating the SKSurface it is important to specify a pixel geometry
            // A defined pixel geometry is required for some anti-aliasing algorithms such as ClearType
            // Also see: https://github.com/AvaloniaUI/Avalonia/pull/9558
            using var surface = SKSurface.Create(info, framebuffer.Address, framebuffer.RowBytes, properties);

            var canvas = surface.Canvas;
            var mat = SKMatrix.CreateScaleTranslation((float)Zoom, (float)Zoom, (float)-OffsetX, (float)-OffsetY);
            canvas.SetMatrix(mat);

            var setupTime = _renderStopwatch.Elapsed;
            OnPaintSurface(new SKPaintSurfaceEventArgs(surface, info, info));
            var totalTime = _renderStopwatch.Elapsed;
            var renderTime = totalTime - setupTime;
            _renderStopwatch.Stop();

            if (ShowFrameTimings)
                RenderFrameTimings(surface, setupTime, renderTime, totalTime);

            _isDirty = false;
        }
        InvalidateVisual();

        unsafe void ClearBitmap(ILockedFramebuffer framebuffer, int imageHeight)
        {
            var ptr = framebuffer.Address.ToPointer();
            uint size = (uint)(framebuffer.RowBytes * imageHeight);
            Unsafe.InitBlock(ptr, 0, size);
        }
    }

    /// <summary>
    /// Renders performance metrics as an overlay
    /// </summary>
    /// <param name="surface">Surface to draw onto</param>
    /// <param name="setupTime">Time between RepaintSurface starting and OnPaintSurface being called</param>
    /// <param name="renderTime">Time taken by OnPaintSurface</param>
    /// <param name="totalTime">Total time to render a frame</param>
    protected virtual void RenderFrameTimings(SKSurface surface, TimeSpan setupTime, TimeSpan renderTime, TimeSpan totalTime)
    {
        _frameTextFill ??= new SKPaint()
        {
            Color = SKColors.Green,
            IsAntialias = true,
        };

        _frameTextFont ??= new SKFont()
        {
            Size = 24 //, Edging = SKFontEdging.SubpixelAntialias
        };

        var canvas = surface.Canvas;
        canvas.DrawText($"Setup: {setupTime.TotalMilliseconds}ms Render: {renderTime.TotalMilliseconds}ms Total: {totalTime.TotalMilliseconds}ms", 0, 0, _frameTextFont, _frameTextFill);
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Invalidate();
    }

    /// <summary>
    /// Called when the canvas should repaint its surface.
    /// </summary>
    /// <param name="e">The event args.</param>
    protected virtual void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        PaintSurface?.Invoke(this, e);
    }
    
    /// <summary>
    /// Called when the app should update its state in preparation for rendering
    /// </summary>
    /// <param name="e">The event args.</param>
    protected virtual void OnUpdateState(UpdateStateEventArgs e)
    {
        UpdateState?.Invoke(this, e);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RenderTriggerProperty)
        {
            if (change.GetNewValue<RenderTrigger>() == RenderTrigger.Continuous)
            {
                _topLevel?.RequestAnimationFrame(PrepareFrame);
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isPanning && AllowPan && _panDragOrigin.HasValue && _panCanvasOrigin.HasValue)
        {
            var point = e.GetCurrentPoint(this);

            OffsetX = _panCanvasOrigin.Value.X - (point.Position.X - _panDragOrigin.Value.X);
            OffsetY = _panCanvasOrigin.Value.Y - (point.Position.Y - _panDragOrigin.Value.Y);
            
            if (EnableConstraints)
            {
                OffsetX = Math.Clamp(OffsetX, MinOffsetX, MaxOffsetX);
                OffsetY = Math.Clamp(OffsetY, MinOffsetY, MaxOffsetY);
            }
            
            e.Handled = true;
            Invalidate();
        }
    }

    /// <summary>
    /// Starts a pan operation with the given origin
    /// </summary>
    public void StartPan(Point panDragOrigin)
    {
        _isPanning = true;
        _panDragOrigin = panDragOrigin;
        _panCanvasOrigin = new(OffsetX, OffsetY);
    }

    /// <summary>
    /// Ends a pan operation
    /// </summary>
    public void EndPan()
    {
        _isPanning = false;
        _panDragOrigin = null;
        _panCanvasOrigin = null;
    }

    /// <summary>
    /// Zooms in while preserving the existing OffsetX and OffsetY
    /// </summary>
    public void ZoomIn()
    {
        if (!AllowZoom)
            return;

        Zoom = Math.Clamp(Zoom * ZoomPower + ZoomIncrement, MinZoom, MaxZoom);
        Invalidate();
    }

    /// <summary>
    /// Zooms out while preserving the existing OffsetX and OffsetY
    /// </summary>
    public void ZoomOut()
    {
        if (!AllowZoom)
            return;

        Zoom = Math.Clamp(Zoom / ZoomPower - ZoomIncrement, MinZoom, MaxZoom);
        Invalidate();
    }

    /// <summary>
    /// Zooms in and centers the viewport around the specified location
    /// </summary>
    public void ZoomInOnCenter(Point center)
    {
        if (!AllowZoom)
            return;

        var local = ScreenToLocalPoint(center);

        var oldZoom = Zoom;
        Zoom = Math.Clamp(Zoom * ZoomPower + ZoomIncrement, MinZoom, MaxZoom);

        if (Math.Abs(oldZoom - Zoom) / Zoom < 0.001)
            return;

        var zoomedLocation = local * Zoom;

        var zoomedWidth = Bounds.Width * oldZoom / Zoom;
        var zoomedHeight = Bounds.Height * oldZoom / Zoom;

        OffsetX = zoomedLocation.X - zoomedWidth;
        OffsetY = zoomedLocation.Y - zoomedHeight;

        Invalidate();
    }

    /// <summary>
    /// Zooms out and centers the viewport around the specified location
    /// </summary>
    public void ZoomOutOnCenter(Point center)
    {
        if (!AllowZoom)
            return;

        var local = ScreenToLocalPoint(center);

        var oldZoom = Zoom;
        Zoom = Math.Clamp(Zoom / ZoomPower - ZoomIncrement, MinZoom, MaxZoom);

        if (Math.Abs(oldZoom - Zoom) / Zoom < 0.001)
            return;

        var zoomedLocation = local * Zoom;

        var zoomedWidth = Bounds.Width * Zoom / oldZoom;
        var zoomedHeight = Bounds.Height * Zoom / oldZoom;

        OffsetX = zoomedLocation.X - zoomedWidth;
        OffsetY = zoomedLocation.Y - zoomedHeight;

        Invalidate();
    }

    /// <summary>
    /// Moves the viewport to a location
    /// </summary>
    /// <param name="location">Specified location in absolute canvas coordinates</param>
    /// <param name="alignment">Positions the Viewport so the specified point appears in a certain spot</param>
    /// <exception cref="NotSupportedException"></exception>
    public void MoveToPoint(Point location, PointAlignment alignment)
    {
        var (lx0, ly0) = location;
        var width = Bounds.Width;
        var height = Bounds.Height;

        (double x, double y) = alignment switch
        {
            PointAlignment.TopLeft => (lx0, ly0),
            PointAlignment.Left => (lx0, ly0 - height * 0.5),
            PointAlignment.BottomLeft => (lx0, ly0 - height),
            PointAlignment.Top => (lx0 - width * 0.5, ly0),
            PointAlignment.Center => (lx0 - width * 0.5, ly0 - height * 0.5),
            PointAlignment.Bottom => (lx0 - width * 0.5, ly0 - height),
            PointAlignment.TopRight => (lx0 - width, ly0),
            PointAlignment.Right => (lx0 - width, ly0 - height * 0.5),
            PointAlignment.BottomRight => (lx0 - width, ly0 - height),
            _ => throw new NotSupportedException()
        };

        OffsetX = x;
        OffsetY = y;

        Invalidate();
    }

    /// <summary>
    /// Transforms a canvas location into a local coordinate (before scaling)
    /// </summary>
    public Point ScreenToLocalPoint(Point screen)
    {
        var x = (screen.X + OffsetX) / Zoom;
        var y = (screen.Y + OffsetY) / Zoom;
        return new Point(x, y);
    }

    /// <summary>
    /// Transforms a canvas location into an absolute canvas coordinate (after scaling)
    /// </summary>
    public Point ScreenToAbsolutePoint(Point screen)
    {
        var x = screen.X + OffsetX;
        var y = screen.Y + OffsetY;
        return new Point(x, y);
    }
}