using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace TileShop.UI.Controls;

/// <summary>
/// A single dialog layer with overlay backdrop and dialog content.
/// </summary>
[TemplatePart(Name = "PART_Backdrop", Type = typeof(Border), IsRequired = true)]
[TemplatePart(Name = "PART_DialogCard", Type = typeof(Border))]
[TemplatePart(Name = "PART_TitleBar", Type = typeof(Border), IsRequired = false)]
[TemplatePart(Name = "PART_CloseButton", Type = typeof(Button), IsRequired = false)]
public partial class OverlayDialog : TemplatedControl
{
    private TaskCompletionSource<bool>? _dialogCompletion;
    private Border? _backdrop;
    private Border? _dialogCard;
    private Border? _titleBar;
    private Button? _closeButton;

    private bool _isDragging;
    private Point _dragStartPoint;

    public OverlayDialog()
    {
        SetCurrentValue(OptionsProperty, []);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        
        _closeButton?.RemoveHandler(Button.ClickEvent, CloseButtonHandler);
        _titleBar?.RemoveHandler(PointerPressedEvent, OnTitleBarPointerPressed);
        _titleBar?.RemoveHandler(PointerMovedEvent, OnTitleBarPointerMoved);
        _titleBar?.RemoveHandler(PointerReleasedEvent, OnTitleBarPointerReleased);
        _titleBar?.RemoveHandler(PointerCaptureLostEvent, OnTitleBarPointerCaptureLost);

        _backdrop = e.NameScope.Get<Border>("PART_Backdrop");
        _dialogCard = e.NameScope.Find<Border>("PART_DialogCard");
        _titleBar = e.NameScope.Find<Border>("PART_TitleBar");
        _closeButton = e.NameScope.Get<Button>("PART_CloseButton");

        _backdrop?.AddHandler(PointerPressedEvent, OnLightDismissPressed);
        _closeButton?.AddHandler(Button.ClickEvent, CloseButtonHandler);
        _titleBar?.AddHandler(PointerPressedEvent, OnTitleBarPointerPressed);
        _titleBar?.AddHandler(PointerMovedEvent, OnTitleBarPointerMoved);
        _titleBar?.AddHandler(PointerReleasedEvent, OnTitleBarPointerReleased);
        _titleBar?.AddHandler(PointerCaptureLostEvent, OnTitleBarPointerCaptureLost);
    }
    
    internal Task<bool> ShowAsync()
    {
        _dialogCompletion = new TaskCompletionSource<bool>();
        _ = AnimateInAsync();
        return _dialogCompletion.Task;
    }

    internal async Task CloseAsync(bool result)
    {
        await AnimateOutAsync();
        _dialogCompletion?.TrySetResult(result);
    }
    
    private async void OnLightDismissPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsLightDismiss)
            return;

        e.Handled = true;
        await CloseAsync(false);
        RaiseEvent(new RoutedEventArgs(DismissEvent));
    }
    
    private async void CloseButtonHandler(object? sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, _closeButton))
        {
            await CloseAsync(false);
            RaiseEvent(new RoutedEventArgs(DismissEvent));
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_dialogCard is null || _backdrop is null)
            return;

        if (!e.GetCurrentPoint(_backdrop).Properties.IsLeftButtonPressed)
            return;

        _isDragging = true;
        _dragStartPoint = e.GetPosition(_backdrop) - new Point(_dialogCard.Margin.Left, _dialogCard.Margin.Top);
        e.Pointer.Capture(_titleBar);
        e.Handled = true;
    }

    private void OnTitleBarPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _dialogCard is null || _backdrop is null)
            return;

        var current = e.GetPosition(_backdrop);
        var deltaX = current.X - _dragStartPoint.X;
        var deltaY = current.Y - _dragStartPoint.Y;

        _dialogCard.Margin = new Thickness(deltaX, deltaY, -deltaX, -deltaY);
        e.Handled = true;
    }

    private void OnTitleBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnTitleBarPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isDragging = false;
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        try
        {
            base.OnKeyDown(e);
            
            if (e.Key == Key.Escape && (ShowCancelButton || ShowCloseButton))
            {
                var cancelOption = Options.FirstOrDefault(o => o.IsCancel);

                if (cancelOption is not null && cancelOption.OptionCommand.CanExecute(null))
                {
                    e.Handled = true;
                    await cancelOption.OptionCommand.ExecuteAsync(null);
                }
                else
                {
                    e.Handled = true;
                    _dialogCompletion?.TrySetCanceled();
                }
                
                _ = CloseAsync(false);
            }
            else if (e.Key == Key.Enter)
            {
                var defaultOption = Options.FirstOrDefault(o => o.IsDefault);
                if (defaultOption is not null && defaultOption.OptionCommand.CanExecute(null))
                {
                    e.Handled = true;
                    await defaultOption.OptionCommand.ExecuteAsync(null);
                    _ = CloseAsync(true);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            throw;
        }
    }

    private async Task AnimateInAsync()
    {
        if (_backdrop is null || _dialogCard is null)
            return;

        _backdrop.SetCurrentValue(OpacityProperty, 0);
        _dialogCard.SetCurrentValue(OpacityProperty, 0);
        _dialogCard.RenderTransform = new Avalonia.Media.ScaleTransform(0.9, 0.9);

        var overlayAnimation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(150),
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0d) } }
            }
        };

        var cardAnimation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters =
                    {
                        new Setter(OpacityProperty, 0.0d),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleXProperty, 0.9d),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleYProperty, 0.9d)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(OpacityProperty, 1.0d),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleXProperty, 1.0d),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleYProperty, 1.0d)
                    }
                }
            }
        };

        await Task.WhenAll(
            overlayAnimation.RunAsync(_backdrop),
            cardAnimation.RunAsync(_dialogCard)
        );
    }

    private async Task AnimateOutAsync()
    {
        if (_backdrop is null || _dialogCard is null)
            return;

        var overlayAnimation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(100),
            Easing = new CubicEaseIn(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 1.0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0.0d) } }
            }
        };

        var cardAnimation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(100),
            Easing = new CubicEaseIn(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters =
                    {
                        new Setter(OpacityProperty, 1.0d),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleXProperty, 1.0d),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleYProperty, 1.0d)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(OpacityProperty, 0.0),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleXProperty, 0.9d),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleYProperty, 0.9d)
                    }
                }
            }
        };

        await Task.WhenAll(
            overlayAnimation.RunAsync(_backdrop),
            cardAnimation.RunAsync(_dialogCard)
        );
    }
}
