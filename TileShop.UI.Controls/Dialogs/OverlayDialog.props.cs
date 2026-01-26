using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Interactivity;
using TileShop.Shared.Interactions;

namespace TileShop.UI.Controls;

public partial class OverlayDialog
{
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<OverlayDialog, object?>(
        nameof(Content));

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<OverlayDialog, string>(nameof(Title), defaultValue: string.Empty);
    
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<RequestOption>> OptionsProperty =
        AvaloniaProperty.Register<OverlayDialog, ObservableCollection<RequestOption>>(nameof(Options));

    public ObservableCollection<RequestOption> Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public static readonly StyledProperty<bool> ShowCancelButtonProperty =
        AvaloniaProperty.Register<OverlayDialog, bool>(nameof(ShowCancelButton), defaultValue: true);
    
    public bool ShowCancelButton
    {
        get => GetValue(ShowCancelButtonProperty);
        set => SetValue(ShowCancelButtonProperty, value);
    }

    public static readonly StyledProperty<DialogMode> ModeProperty =
        AvaloniaProperty.Register<OverlayDialog, DialogMode>(nameof(Mode));
    
    public DialogMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }
    
    public static readonly RoutedEvent<RoutedEventArgs> DismissEvent =
        RoutedEvent.Register<OverlayDialog, RoutedEventArgs>(nameof(Dismiss), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> Dismiss
    {
        add => AddHandler(DismissEvent, value);
        remove => RemoveHandler(DismissEvent, value);
    }

    public static readonly StyledProperty<bool> IsLightDismissProperty =
        AvaloniaProperty.Register<OverlayDialog, bool>(nameof(IsLightDismiss), defaultValue: true);

    public bool IsLightDismiss
    {
        get => GetValue(IsLightDismissProperty);
        set => SetValue(IsLightDismissProperty, value);
    }

    public static readonly StyledProperty<bool> ShowCloseButtonProperty =
        AvaloniaProperty.Register<OverlayDialog, bool>(nameof(ShowCloseButton), defaultValue: true);

    public bool ShowCloseButton
    {
        get => GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }
}