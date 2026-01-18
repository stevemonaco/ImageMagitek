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
[TemplatePart(Name = "PART_AcceptButton", Type = typeof(Button), IsRequired = true)]
[TemplatePart(Name = "PART_RejectButton", Type = typeof(Button), IsRequired = true)]
[TemplatePart(Name = "PART_CancelButton", Type = typeof(Button), IsRequired = true)]
[TemplatePart(Name = "PART_CloseButton", Type = typeof(Button), IsRequired = true)]
public partial class OverlayDialog : TemplatedControl
{
    private TaskCompletionSource<bool>? _dialogCompletion;
    private Border? _backdrop;
    private Border? _dialogCard;
    private Button? _acceptButton;
    private Button? _rejectButton;
    private Button? _cancelButton;
    private Button? _closeButton;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        AddHandler(Button.ClickEvent, Handler);
    }

    private void Handler(object? sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, _acceptButton))
            _ = CloseAsync(true);
        
        if (ReferenceEquals(sender, _rejectButton) || 
            ReferenceEquals(sender, _cancelButton) || 
            ReferenceEquals(sender, _closeButton))
            _ = CloseAsync(false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _backdrop = e.NameScope.Get<Border>("PART_Backdrop");
        _dialogCard = e.NameScope.Find<Border>("PART_DialogCard");
        _acceptButton = e.NameScope.Get<Button>("PART_AcceptButton");
        _rejectButton = e.NameScope.Get<Button>("PART_RejectButton");
        _cancelButton = e.NameScope.Get<Button>("PART_CancelButton");
        _closeButton = e.NameScope.Get<Button>("PART_CloseButton");

        _backdrop.PointerPressed += OnLightDismiss;
    }
    
    internal Task<bool> ShowAsync()
    {
        _dialogCompletion = new TaskCompletionSource<bool>();
        IsVisible = true;
        _ = AnimateInAsync();
        return _dialogCompletion.Task;
    }

    internal async Task CloseAsync(bool result)
    {
        await AnimateOutAsync();
        IsVisible = false;
        _dialogCompletion?.TrySetResult(result);
    }
    
    private async void OnLightDismiss(object? sender, PointerPressedEventArgs e)
    {
        if (!IsLightDismiss)
            return;

        e.Handled = true;
        await CloseAsync(false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape && (ShowCancelButton || ShowCloseButton))
        {
            CancelCommand?.Execute(null);
            _ = CloseAsync(false);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && IsAcceptEnabled)
        {
            AcceptCommand?.Execute(null);
            _ = CloseAsync(true);
            e.Handled = true;
        }
    }

    private async Task AnimateInAsync()
    {
        if (_backdrop is null || _dialogCard is null)
            return;

        _backdrop.Opacity = 0;
        _dialogCard.Opacity = 0;
        _dialogCard.RenderTransform = new Avalonia.Media.ScaleTransform(0.9, 0.9);

        var overlayAnimation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(150),
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0) } }
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
                        new Setter(OpacityProperty, 0.0),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleXProperty, 0.9),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleYProperty, 0.9)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(OpacityProperty, 1.0),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleXProperty, 1.0),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleYProperty, 1.0)
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
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 1.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0.0) } }
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
                        new Setter(OpacityProperty, 1.0),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleXProperty, 1.0),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleYProperty, 1.0)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(OpacityProperty, 0.0),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleXProperty, 0.9),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleYProperty, 0.9)
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
