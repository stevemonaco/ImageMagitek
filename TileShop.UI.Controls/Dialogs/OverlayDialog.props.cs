using System.Collections.ObjectModel;
using Avalonia;
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

    // public static readonly StyledProperty<string> AcceptTextProperty =
    //     AvaloniaProperty.Register<OverlayDialog, string>(nameof(AcceptText), defaultValue: "Ok");
    //
    // public string AcceptText
    // {
    //     get => GetValue(AcceptTextProperty);
    //     set => SetValue(AcceptTextProperty, value);
    // }
    //
    // public static readonly StyledProperty<string> RejectTextProperty =
    //     AvaloniaProperty.Register<OverlayDialog, string>(nameof(RejectText), defaultValue: "Reject");
    //
    // public string? RejectText
    // {
    //     get => GetValue(RejectTextProperty);
    //     set => SetValue(RejectTextProperty, value);
    // }
    //
    // public static readonly StyledProperty<string> CancelTextProperty =
    //     AvaloniaProperty.Register<OverlayDialog, string>(nameof(CancelText), defaultValue: "Cancel");
    //
    // public string CancelText
    // {
    //     get => GetValue(CancelTextProperty);
    //     set => SetValue(CancelTextProperty, value);
    // }
    //
    // public static readonly StyledProperty<ICommand?> AcceptCommandProperty =
    //     AvaloniaProperty.Register<OverlayDialog, ICommand?>(nameof(AcceptCommand));
    //
    // public ICommand? AcceptCommand
    // {
    //     get => GetValue(AcceptCommandProperty);
    //     set => SetValue(AcceptCommandProperty, value);
    // }
    //
    // public static readonly StyledProperty<ICommand?> RejectCommandProperty =
    //     AvaloniaProperty.Register<OverlayDialog, ICommand?>(nameof(RejectCommand));
    //
    // public ICommand? RejectCommand
    // {
    //     get => GetValue(RejectCommandProperty);
    //     set => SetValue(RejectCommandProperty, value);
    // }
    //
    // public static readonly StyledProperty<ICommand?> CancelCommandProperty =
    //     AvaloniaProperty.Register<OverlayDialog, ICommand?>(nameof(CancelCommand));
    //
    // public ICommand? CancelCommand
    // {
    //     get => GetValue(CancelCommandProperty);
    //     set => SetValue(CancelCommandProperty, value);
    // }
    //
    // public static readonly StyledProperty<bool> IsAcceptEnabledProperty =
    //     AvaloniaProperty.Register<OverlayDialog, bool>(nameof(IsAcceptEnabled), defaultValue: true);
    //
    // public bool IsAcceptEnabled
    // {
    //     get => GetValue(IsAcceptEnabledProperty);
    //     set => SetValue(IsAcceptEnabledProperty, value);
    // }

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