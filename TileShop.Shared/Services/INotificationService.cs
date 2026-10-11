using System.Collections.ObjectModel;
using System.ComponentModel;
using TileShop.Shared.Models;

namespace TileShop.Shared.Services;

/// <summary>
/// Session-only list of notifications shown in the status bar. Post may be called from any thread.
/// </summary>
public interface INotificationService : INotifyPropertyChanged
{
    /// <summary>
    /// Notifications, newest first
    /// </summary>
    ReadOnlyObservableCollection<Notification> Notifications { get; }

    int UnreadCount { get; }

    /// <summary>
    /// The most severe unread notification's severity, or null when nothing is unread
    /// </summary>
    NotificationSeverity? HighestUnreadSeverity { get; }

    void Post(NotificationSeverity severity, string source, string title, string? detail = null);
    void MarkAllRead();
    void Dismiss(Notification notification);
    void ClearAll();
}
