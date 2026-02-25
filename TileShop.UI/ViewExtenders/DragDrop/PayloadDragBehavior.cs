using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactions.DragAndDrop;
using Avalonia.Xaml.Interactivity;
using TileShop.UI.ViewExtenders.DragDrop;

namespace TileShop.UI.DragDrop;

using DragDrop = Avalonia.Input.DragDrop;

public class PayloadDragBehavior : Behavior<Control>
{
    private Point _dragStartPoint;
    private PointerEventArgs? _triggerEvent;
    private bool _lock;

    public static readonly StyledProperty<object?> ContextProperty =
        AvaloniaProperty.Register<ContextDragBehavior, object?>(nameof(Context));

    public static readonly StyledProperty<IDragHandlerEx?> HandlerProperty =
        AvaloniaProperty.Register<ContextDragBehavior, IDragHandlerEx?>(nameof(Handler));

    public static readonly StyledProperty<double> HorizontalDragThresholdProperty =
        AvaloniaProperty.Register<ContextDragBehavior, double>(nameof(HorizontalDragThreshold), 3);

    public static readonly StyledProperty<double> VerticalDragThresholdProperty =
        AvaloniaProperty.Register<ContextDragBehavior, double>(nameof(VerticalDragThreshold), 3);

    public object? Context
    {
        get => GetValue(ContextProperty);
        set => SetValue(ContextProperty, value);
    }

    public IDragHandlerEx? Handler
    {
        get => GetValue(HandlerProperty);
        set => SetValue(HandlerProperty, value);
    }

    public double HorizontalDragThreshold
    {
        get => GetValue(HorizontalDragThresholdProperty);
        set => SetValue(HorizontalDragThresholdProperty, value);
    }

    public double VerticalDragThreshold
    {
        get => GetValue(VerticalDragThresholdProperty);
        set => SetValue(VerticalDragThresholdProperty, value);
    }

    protected override void OnAttachedToVisualTree()
    {
        AssociatedObject?.AddHandler(InputElement.PointerPressedEvent, AssociatedObject_PointerPressed, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AssociatedObject?.AddHandler(InputElement.PointerReleasedEvent, AssociatedObject_PointerReleased, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AssociatedObject?.AddHandler(InputElement.PointerMovedEvent, AssociatedObject_PointerMoved, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    protected override void OnDetachedFromVisualTree()
    {
        AssociatedObject?.RemoveHandler(InputElement.PointerPressedEvent, AssociatedObject_PointerPressed);
        AssociatedObject?.RemoveHandler(InputElement.PointerReleasedEvent, AssociatedObject_PointerReleased);
        AssociatedObject?.RemoveHandler(InputElement.PointerMovedEvent, AssociatedObject_PointerMoved);
    }

    private async Task DoDragDrop<T>(PointerEventArgs triggerEvent, T value)
        where T : class
    {
        var effect = DragDropEffects.None;

        if (triggerEvent.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            effect |= DragDropEffects.Link;
        }
        else if (triggerEvent.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            effect |= DragDropEffects.Move;
        }
        else if (triggerEvent.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            effect |= DragDropEffects.Copy;
        }
        else
        {
            effect |= DragDropEffects.Move;
        }

        var dataTransfer = IDataTransfer.CreateInProcessTransfer(value);
        await DragDrop.DoDragDropAsync(triggerEvent, dataTransfer, effect);
    }

    private void AssociatedObject_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(AssociatedObject).Properties;
        if (properties.IsLeftButtonPressed)
        {
            if (e.Source is Control control
                && AssociatedObject?.DataContext == control.DataContext)
            {
                _dragStartPoint = e.GetPosition(null);
                _triggerEvent = e;
                _lock = true;
                e.Pointer.Capture(AssociatedObject);
            }
        }
    }

    private void AssociatedObject_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (Equals(e.Pointer.Captured, AssociatedObject))
        {
            if (e.InitialPressMouseButton == MouseButton.Left && _triggerEvent is { })
            {
                _triggerEvent = null;
                _lock = false;
            }

            e.Pointer.Capture(null);
        }
    }

    private async void AssociatedObject_PointerMoved(object? sender, PointerEventArgs e)
    {
        var properties = e.GetCurrentPoint(AssociatedObject).Properties;
        if (Equals(e.Pointer.Captured, AssociatedObject)
            && properties.IsLeftButtonPressed &&
            _triggerEvent is { })
        {
            var point = e.GetPosition(null);
            var diff = _dragStartPoint - point;
            var horizontalDragThreshold = HorizontalDragThreshold;
            var verticalDragThreshold = VerticalDragThreshold;

            if (Math.Abs(diff.X) > horizontalDragThreshold || Math.Abs(diff.Y) > verticalDragThreshold)
            {
                if (_lock)
                {
                    _lock = false;
                }
                else
                {
                    return;
                }

                var context = Context ?? AssociatedObject?.DataContext;

                Handler?.BeforeDragDrop(sender, _triggerEvent, context);

                var payload = Handler?.Payload ?? context;

                await DoDragDrop(_triggerEvent, payload);

                Handler?.AfterDragDrop(sender, _triggerEvent, payload);

                _triggerEvent = null;
            }
        }
    }
}