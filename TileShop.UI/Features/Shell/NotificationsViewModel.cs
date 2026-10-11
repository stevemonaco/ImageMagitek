using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileShop.Shared.Models;
using TileShop.Shared.Services;

namespace TileShop.UI.ViewModels;

/// <summary>
/// State of the status bar's notification button and its flyout.
/// </summary>
public partial class NotificationsViewModel : ObservableObject
{
    private readonly INotificationService _service;

    public ReadOnlyObservableCollection<Notification> Notifications => _service.Notifications;
    public int UnreadCount => _service.UnreadCount;
    public NotificationSeverity? IconSeverity => _service.HighestUnreadSeverity;

    /// <summary>
    /// Set when a new Error entry arrives; the view plays a one-time highlight on each rising edge
    /// </summary>
    [ObservableProperty] private bool _isFlashing;

    public NotificationsViewModel(INotificationService service)
    {
        _service = service;
        _service.PropertyChanged += OnServicePropertyChanged;
        ((INotifyCollectionChanged)_service.Notifications).CollectionChanged += OnNotificationsChanged;
    }

    private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(INotificationService.UnreadCount))
            OnPropertyChanged(nameof(UnreadCount));
        else if (e.PropertyName == nameof(INotificationService.HighestUnreadSeverity))
            OnPropertyChanged(nameof(IconSeverity));
    }

    // A collapsed repeat only moves its entry, so Add means a new entry
    private void OnNotificationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems![0] is not Notification { Severity: NotificationSeverity.Error })
            return;

        IsFlashing = false;
        IsFlashing = true;
    }

    [RelayCommand]
    private void Open()
    {
        _service.MarkAllRead();
        IsFlashing = false;
    }

    [RelayCommand]
    private void ClearAll() => _service.ClearAll();

    [RelayCommand]
    private void Dismiss(Notification notification) => _service.Dismiss(notification);
}
