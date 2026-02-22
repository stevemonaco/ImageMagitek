using Avalonia;
using Avalonia.Data;

namespace TileShop.UI.Controls;
public partial class InfiniteCanvas
{
    static InfiniteCanvas()
    {
        AffectsRender<InfiniteCanvas>(ShowFrameTimingsProperty, OffsetXProperty, OffsetYProperty, ZoomProperty);
    }

    protected bool _isPanning;
    protected Point? _panDragOrigin;
    protected Point? _panCanvasOrigin;
    private double _zoomIncrement = 0d;
    private double _zoomPower = 2d;
    private double _offsetX;
    private double _offsetY;
    private bool _captured = false;

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(Zoom), 1d, false, BindingMode.TwoWay);

    public static readonly DirectProperty<InfiniteCanvas, double> ZoomPowerProperty =
        AvaloniaProperty.RegisterDirect<InfiniteCanvas, double>(nameof(ZoomPower), o => o._zoomPower, (o, v) => o._zoomPower = v, 2.0);

    public static readonly DirectProperty<InfiniteCanvas, double> ZoomIncrementProperty =
        AvaloniaProperty.RegisterDirect<InfiniteCanvas, double>(nameof(ZoomIncrement), o => o._zoomIncrement, (o, v) => o._zoomIncrement = v, 0.0);

    public static readonly DirectProperty<InfiniteCanvas, double> OffsetXProperty =
        AvaloniaProperty.RegisterDirect<InfiniteCanvas, double>(nameof(OffsetX), o => o.OffsetX, null, 0.0);

    public static readonly DirectProperty<InfiniteCanvas, double> OffsetYProperty =
        AvaloniaProperty.RegisterDirect<InfiniteCanvas, double>(nameof(OffsetY), o => o.OffsetY, null, 0.0);

    public static readonly StyledProperty<bool> EnableConstraintsProperty =
        AvaloniaProperty.Register<InfiniteCanvas, bool>(nameof(EnableConstraints), true, false, BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinZoomProperty =
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(MinZoom), double.NegativeInfinity, false, BindingMode.TwoWay);

    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(MaxZoom), double.PositiveInfinity, false, BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinOffsetXProperty =
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(MinOffsetX), double.NegativeInfinity, false, BindingMode.TwoWay);

    public static readonly StyledProperty<double> MaxOffsetXProperty =
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(MaxOffsetX), double.PositiveInfinity, false, BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinOffsetYProperty =
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(MinOffsetY), double.NegativeInfinity, false, BindingMode.TwoWay);

    public static readonly StyledProperty<double> MaxOffsetYProperty =
        AvaloniaProperty.Register<InfiniteCanvas, double>(nameof(MaxOffsetY), double.PositiveInfinity, false, BindingMode.TwoWay);

    public static readonly StyledProperty<bool> AllowPanProperty =
        AvaloniaProperty.Register<InfiniteCanvas, bool>(nameof(AllowPan), true, false, BindingMode.TwoWay);

    public static readonly StyledProperty<bool> AllowZoomProperty =
        AvaloniaProperty.Register<InfiniteCanvas, bool>(nameof(AllowZoom), true, false, BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowFrameTimingsProperty =
        AvaloniaProperty.Register<InfiniteCanvas, bool>(nameof(ShowFrameTimings));

    public static readonly StyledProperty<RenderTrigger> RenderTriggerProperty =
        AvaloniaProperty.Register<InfiniteCanvas, RenderTrigger>(nameof(ShowFrameTimings), RenderTrigger.Invalidation);

    /// <summary>
    /// Gets or sets the zoom ratio.
    /// </summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>
    /// Gets or sets the zoom power (multiplier).
    /// </summary>
    public double ZoomPower
    {
        get => _zoomPower;
        set => SetAndRaise(ZoomPowerProperty, ref _zoomPower, value);
    }

    /// <summary>
    /// Gets or sets the zoom increment (addition).
    /// </summary>
    public double ZoomIncrement
    {
        get => _zoomIncrement;
        set => SetAndRaise(ZoomIncrementProperty, ref _zoomIncrement, value);
    }

    /// <summary>
    /// Gets or sets the pan offset for x-axis in zoomed coordinates from the rendered content to the left edge of the canvas.
    /// </summary>
    public double OffsetX
    {
        get => _offsetX;
        set => SetAndRaise(OffsetXProperty, ref _offsetX, value);
    }

    /// <summary>
    /// Gets or sets the pan offset for y-axis in zoomed coordinates from the rendered content to the left edge of the canvas.
    /// </summary>
    public double OffsetY
    {
        get => _offsetY;
        set => SetAndRaise(OffsetYProperty, ref _offsetY, value);
    }

    /// <summary>
    /// Gets or sets flag indicating whether zoom ratio and pan offset constraints are applied.
    /// </summary>
    public bool EnableConstraints
    {
        get => GetValue(EnableConstraintsProperty);
        set => SetValue(EnableConstraintsProperty, value);
    }

    /// <summary>
    /// Gets or sets minimum zoom ratio
    /// </summary>
    public double MinZoom
    {
        get => GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    /// <summary>
    /// Gets or sets maximum zoom ratio
    /// </summary>
    public double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    /// <summary>
    /// Gets or sets minimum offset for x-axis.
    /// </summary>
    public double MinOffsetX
    {
        get => GetValue(MinOffsetXProperty);
        set => SetValue(MinOffsetXProperty, value);
    }

    /// <summary>
    /// Gets or sets maximum offset for x-axis.
    /// </summary>
    public double MaxOffsetX
    {
        get => GetValue(MaxOffsetXProperty);
        set => SetValue(MaxOffsetXProperty, value);
    }

    /// <summary>
    /// Gets or sets minimum offset for y-axis.
    /// </summary>
    public double MinOffsetY
    {
        get => GetValue(MinOffsetYProperty);
        set => SetValue(MinOffsetYProperty, value);
    }

    /// <summary>
    /// Gets or sets maximum offset for y-axis.
    /// </summary>
    public double MaxOffsetY
    {
        get => GetValue(MaxOffsetYProperty);
        set => SetValue(MaxOffsetYProperty, value);
    }

    /// <summary>
    /// Gets or sets flag indicating whether pan input events are processed.
    /// </summary>
    public bool AllowPan
    {
        get => GetValue(AllowPanProperty);
        set => SetValue(AllowPanProperty, value);
    }

    /// <summary>
    /// Gets or sets flag indicating whether input zoom events are processed.
    /// </summary>
    public bool AllowZoom
    {
        get => GetValue(AllowZoomProperty);
        set => SetValue(AllowZoomProperty, value);
    }

    /// <summary>
    /// Gets or sets flag indicating whether frame timings are rendered.
    /// </summary>
    public bool ShowFrameTimings
    {
        get => GetValue(ShowFrameTimingsProperty);
        set => SetValue(ShowFrameTimingsProperty, value);
    }

    /// <summary>
    /// Gets or sets trigger mechanism for rendering
    /// </summary>
    public RenderTrigger RenderTrigger
    {
        get => GetValue(RenderTriggerProperty);
        set => SetValue(RenderTriggerProperty, value);
    }
}