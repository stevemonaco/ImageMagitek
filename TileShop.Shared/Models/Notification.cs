using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.Shared.Models;

/// <summary>
/// Severity of a notification, ordered so a higher value is more severe.
/// </summary>
public enum NotificationSeverity { Info, Warning, Error }

/// <summary>
/// One entry in the notification area. Repeats with the same source and title collapse into it.
/// </summary>
public partial class Notification : ObservableObject
{
    public required NotificationSeverity Severity { get; init; }
    public required string Source { get; init; }
    public required string Title { get; init; }
    public string? Detail { get; init; }

    [ObservableProperty] private DateTimeOffset _lastTime;
    [ObservableProperty] private int _count = 1;
    public bool IsRead { get; set; }
}
