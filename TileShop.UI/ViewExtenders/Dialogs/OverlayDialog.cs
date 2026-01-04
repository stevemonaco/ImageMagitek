using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.UI.ViewExtenders.Dialogs;

/// <summary>
/// A single dialog layer with overlay backdrop and dialog content.
/// </summary>
public class OverlayDialog : TemplatedControl
{
    private TaskCompletionSource<bool>? _dialogCompletion;
    private IRelayCommand? _acceptCommand;
    private IRelayCommand? _cancelCommand;
    private Border? _overlay;
    private Border? _dialogCard;
    private Button? _acceptButton;
    private Button? _cancelButton;

    public static readonly StyledProperty<Control?> DialogContentProperty =
        AvaloniaProperty.Register<OverlayDialog, Control?>(nameof(DialogContent));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<OverlayDialog, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> AcceptTextProperty =
        AvaloniaProperty.Register<OverlayDialog, string>(nameof(AcceptText), "Ok");

    public static readonly StyledProperty<string> CancelTextProperty =
        AvaloniaProperty.Register<OverlayDialog, string>(nameof(CancelText), "Cancel");

    public static readonly StyledProperty<bool> IsAcceptEnabledProperty =
        AvaloniaProperty.Register<OverlayDialog, bool>(nameof(IsAcceptEnabled), true);

    public static readonly StyledProperty<bool> ShowCancelButtonProperty =
        AvaloniaProperty.Register<OverlayDialog, bool>(nameof(ShowCancelButton), true);

    public static readonly StyledProperty<DialogMode> ModeProperty =
        AvaloniaProperty.Register<OverlayDialog, DialogMode>(nameof(Mode), DialogMode.None);

    public Control? DialogContent
    {
        get => GetValue(DialogContentProperty);
        set => SetValue(DialogContentProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string AcceptText
    {
        get => GetValue(AcceptTextProperty);
        set => SetValue(AcceptTextProperty, value);
    }

    public string CancelText
    {
        get => GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    public bool IsAcceptEnabled
    {
        get => GetValue(IsAcceptEnabledProperty);
        set => SetValue(IsAcceptEnabledProperty, value);
    }

    public bool ShowCancelButton
    {
        get => GetValue(ShowCancelButtonProperty);
        set => SetValue(ShowCancelButtonProperty, value);
    }

    public DialogMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _overlay = e.NameScope.Find<Border>("PART_Overlay");
        _dialogCard = e.NameScope.Find<Border>("PART_DialogCard");
        _acceptButton = e.NameScope.Find<Button>("PART_AcceptButton");
        _cancelButton = e.NameScope.Find<Button>("PART_CancelButton");

        if (_acceptButton is not null)
            _acceptButton.Click += OnAcceptClick;

        if (_cancelButton is not null)
            _cancelButton.Click += OnCancelClick;
    }

    internal void BindCommands(IRelayCommand? acceptCommand, IRelayCommand? cancelCommand)
    {
        _acceptCommand = acceptCommand;
        _cancelCommand = cancelCommand;

        if (_acceptCommand is not null)
        {
            _acceptCommand.CanExecuteChanged += OnAcceptCanExecuteChanged;
            IsAcceptEnabled = _acceptCommand.CanExecute(null);
        }
    }

    internal void UnbindCommands()
    {
        if (_acceptCommand is not null)
        {
            _acceptCommand.CanExecuteChanged -= OnAcceptCanExecuteChanged;
        }
        _acceptCommand = null;
        _cancelCommand = null;
    }

    private void OnAcceptCanExecuteChanged(object? sender, EventArgs e)
    {
        IsAcceptEnabled = _acceptCommand?.CanExecute(null) ?? true;
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

    private void OnAcceptClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _acceptCommand?.Execute(null);
        _ = CloseAsync(true);
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cancelCommand?.Execute(null);
        _ = CloseAsync(false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape && ShowCancelButton)
        {
            _cancelCommand?.Execute(null);
            _ = CloseAsync(false);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && IsAcceptEnabled)
        {
            _acceptCommand?.Execute(null);
            _ = CloseAsync(true);
            e.Handled = true;
        }
    }

    private async Task AnimateInAsync()
    {
        if (_overlay is null || _dialogCard is null)
            return;

        _overlay.Opacity = 0;
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
            overlayAnimation.RunAsync(_overlay),
            cardAnimation.RunAsync(_dialogCard)
        );
    }

    private async Task AnimateOutAsync()
    {
        if (_overlay is null || _dialogCard is null)
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
            overlayAnimation.RunAsync(_overlay),
            cardAnimation.RunAsync(_dialogCard)
        );
    }
}
