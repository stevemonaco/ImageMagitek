using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TileShop.Shared.Interactions;

namespace TileShop.UI.Controls;

/// <summary>
/// A single dialog layer with overlay backdrop and dialog content.
/// </summary>
[TemplatePart(Name = "PART_Backdrop", Type = typeof(Border), IsRequired = true)]
[TemplatePart(Name = "PART_DialogCard", Type = typeof(Border))]
[TemplatePart(Name = "PART_TitleBar", Type = typeof(Border), IsRequired = false)]
[TemplatePart(Name = "PART_CloseButton", Type = typeof(Button), IsRequired = false)]
[TemplatePart(Name = "PART_Content", Type = typeof(ContentPresenter), IsRequired = false)]
[TemplatePart(Name = "PART_Options", Type = typeof(ItemsControl), IsRequired = false)]
public partial class OverlayDialog : TemplatedControl
{
    private Border? _backdrop;
    private Border? _dialogCard;
    private Border? _titleBar;
    private Button? _closeButton;
    private ContentPresenter? _content;
    private ItemsControl? _options;
    private IInputElement? _previouslyFocused;

    private bool _isDragging;
    private Point _dragStartPoint;
    private bool _isClosing;

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
        _content = e.NameScope.Find<ContentPresenter>("PART_Content");
        _options = e.NameScope.Find<ItemsControl>("PART_Options");

        _backdrop?.AddHandler(PointerPressedEvent, OnLightDismissPressed);
        _closeButton?.AddHandler(Button.ClickEvent, CloseButtonHandler);
        _titleBar?.AddHandler(PointerPressedEvent, OnTitleBarPointerPressed);
        _titleBar?.AddHandler(PointerMovedEvent, OnTitleBarPointerMoved);
        _titleBar?.AddHandler(PointerReleasedEvent, OnTitleBarPointerReleased);
        _titleBar?.AddHandler(PointerCaptureLostEvent, OnTitleBarPointerCaptureLost);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _previouslyFocused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        FocusInitialElement();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Deferred so the restore runs after the removal has fully completed and the dialog's focused child is gone
        if (_previouslyFocused is Visual previous)
        {
            var target = _previouslyFocused;
            Dispatcher.UIThread.Post(() =>
            {
                if (previous.IsAttachedToVisualTree())
                    target.Focus();
            }, DispatcherPriority.Input);
        }

        _previouslyFocused = null;
    }

    /// <summary>
    /// Moves keyboard focus into the dialog unless the content already claimed it, preferring the first text input
    /// </summary>
    private void FocusInitialElement()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is Visual current && this.IsVisualAncestorOf(current))
            return;

        var contentTargets = _content?.GetVisualDescendants().OfType<Control>().Where(IsFocusTarget).ToList() ?? [];

        Control target = contentTargets.OfType<TextBox>().FirstOrDefault()
            ?? contentTargets.FirstOrDefault()
            ?? (Control?)FindDefaultOptionButton()
            ?? this;

        target.Focus();
    }

    private Button? FindDefaultOptionButton() =>
        _options?.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.DataContext is RequestOption { IsDefault: true } && IsFocusTarget(b));

    private static bool IsFocusTarget(Control control) =>
        control.Focusable && control.IsTabStop && control.IsEffectivelyEnabled && control.IsEffectivelyVisible;

    private void OnLightDismissPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsLightDismiss)
            return;

        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(DismissEvent));
    }

    private void CloseButtonHandler(object? sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, _closeButton))
            RaiseEvent(new RoutedEventArgs(DismissEvent));
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_dialogCard is null || _backdrop is null || Size is DialogSize.Large or DialogSize.Full)
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

        var backdropWidth = _backdrop.Bounds.Width;
        var backdropHeight = _backdrop.Bounds.Height;
        var cardWidth = _dialogCard.Bounds.Width;
        var cardHeight = _dialogCard.Bounds.Height;
        var titleBarHeight = _titleBar?.Bounds.Height ?? 40;

        // The dialog is centered by default, so margin offsets are relative to center.
        var centeredLeft = (backdropWidth - cardWidth) / 2;
        var centeredTop = (backdropHeight - cardHeight) / 2;

        // Clamp so the dialog stays within the backdrop horizontally,
        // and the title bar stays within the backdrop vertically.
        var minX = -centeredLeft;
        var maxX = backdropWidth - cardWidth - centeredLeft;
        var minY = -centeredTop;
        var maxY = backdropHeight - titleBarHeight - centeredTop;

        deltaX = Math.Clamp(deltaX, minX, maxX);
        deltaY = Math.Clamp(deltaY, minY, maxY);

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

            if (_isClosing)
                return;

            var (kind, option) = DialogKeyAction.Resolve(e.Key, Options);
            if (kind != DialogKeyActionKind.None || e.Key == Key.Escape)
                e.Handled = true;

            if (kind == DialogKeyActionKind.RunOption)
                await option!.OptionCommand.ExecuteAsync(null);
            else if (kind == DialogKeyActionKind.Dismiss)
                RaiseEvent(new RoutedEventArgs(DismissEvent));
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            throw;
        }
    }

    private double ScaleOutValue => Size == DialogSize.Full ? 1.0 : 0.9;

    internal async Task AnimateOutAsync()
    {
        _isClosing = true;

        if (_backdrop is null || _dialogCard is null)
            return;

        var startScale = ScaleOutValue;

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
                        new Setter(Avalonia.Media.ScaleTransform.ScaleXProperty, startScale),
                        new Setter(Avalonia.Media.ScaleTransform.ScaleYProperty, startScale)
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
