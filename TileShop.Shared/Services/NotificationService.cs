using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using TileShop.Shared.Models;

namespace TileShop.Shared.Services;

/// <summary>
/// Keeps the newest <see cref="MaximumCount"/> notifications, collapsing repeats by source and title, and logs every post.
/// </summary>
public sealed partial class NotificationService : ObservableObject, INotificationService
{
    public const int MaximumCount = 200;

    private readonly ILogger<NotificationService> _logger;
    private readonly Action<Action> _dispatch;
    private readonly ObservableCollection<Notification> _notifications = [];

    public ReadOnlyObservableCollection<Notification> Notifications { get; }

    [ObservableProperty] private int _unreadCount;
    [ObservableProperty] private NotificationSeverity? _highestUnreadSeverity;

    /// <param name="dispatch">Runs an action on the UI thread; state changes only happen inside it</param>
    public NotificationService(ILogger<NotificationService> logger, Action<Action> dispatch)
    {
        _logger = logger;
        _dispatch = dispatch;
        Notifications = new(_notifications);
    }

    public void Post(NotificationSeverity severity, string source, string title, string? detail = null)
    {
        var level = severity switch
        {
            NotificationSeverity.Error => LogLevel.Error,
            NotificationSeverity.Warning => LogLevel.Warning,
            _ => LogLevel.Information
        };
        if (detail is null)
            _logger.Log(level, "{Source}: {Title}", source, title);
        else
            _logger.Log(level, "{Source}: {Title}{NewLine}{Detail}", source, title, Environment.NewLine, detail);

        var now = DateTimeOffset.Now;
        _dispatch(() => Apply(severity, source, title, detail, now));
    }

    private void Apply(NotificationSeverity severity, string source, string title, string? detail, DateTimeOffset now)
    {
        var existing = _notifications.FirstOrDefault(x => x.Source == source && x.Title == title);
        if (existing is not null)
        {
            existing.Count++;
            existing.LastTime = now;
            existing.IsRead = false;
            _notifications.Move(_notifications.IndexOf(existing), 0);
            UpdateUnread();
            return;
        }

        var notification = new Notification
        {
            Severity = severity,
            Source = source,
            Title = title,
            Detail = detail,
            LastTime = now
        };

        _notifications.Insert(0, notification);
        while (_notifications.Count > MaximumCount)
            _notifications.RemoveAt(_notifications.Count - 1);

        UpdateUnread();
    }

    public void MarkAllRead()
    {
        foreach (var notification in _notifications)
            notification.IsRead = true;

        UpdateUnread();
    }

    public void Dismiss(Notification notification)
    {
        _notifications.Remove(notification);
        UpdateUnread();
    }

    public void ClearAll()
    {
        _notifications.Clear();
        UpdateUnread();
    }

    private void UpdateUnread()
    {
        var unread = _notifications.Where(x => !x.IsRead).ToList();
        UnreadCount = unread.Count;
        HighestUnreadSeverity = unread.Count == 0 ? null : unread.Max(x => x.Severity);
    }
}
