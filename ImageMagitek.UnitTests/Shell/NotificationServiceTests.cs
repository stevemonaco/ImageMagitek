using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TileShop.Shared.Models;
using TileShop.Shared.Services;
using Xunit;

namespace ImageMagitek.UnitTests.Shell;

public class NotificationServiceTests
{
    private readonly NotificationService _service = new(NullLogger<NotificationService>.Instance, a => a());

    [Fact]
    public void Post_SameSourceAndTitle_IncrementsCountKeepsFirstDetail()
    {
        _service.Post(NotificationSeverity.Warning, "Plugins", "Failed", "first");
        _service.Post(NotificationSeverity.Info, "Other", "Unrelated");
        _service.Post(NotificationSeverity.Warning, "Plugins", "Failed", "second");

        Assert.Equal(2, _service.Notifications.Count);
        var collapsed = _service.Notifications[0];
        Assert.Equal("Failed", collapsed.Title);
        Assert.Equal(2, collapsed.Count);
        Assert.Equal("first", collapsed.Detail);
    }

    [Fact]
    public void Post_OverCap_DropsOldest()
    {
        for (var i = 0; i <= NotificationService.MaximumCount; i++)
            _service.Post(NotificationSeverity.Info, "Test", $"Item {i}");

        Assert.Equal(NotificationService.MaximumCount, _service.Notifications.Count);
        Assert.Equal($"Item {NotificationService.MaximumCount}", _service.Notifications[0].Title);
        Assert.DoesNotContain(_service.Notifications, x => x.Title == "Item 0");
    }

    [Fact]
    public void UnreadCountAndHighestSeverity_FollowUnreadOnly()
    {
        Assert.Null(_service.HighestUnreadSeverity);

        _service.Post(NotificationSeverity.Error, "Test", "Error");
        _service.MarkAllRead();
        _service.Post(NotificationSeverity.Info, "Test", "Info");
        _service.Post(NotificationSeverity.Warning, "Test", "Warning");

        Assert.Equal(2, _service.UnreadCount);
        Assert.Equal(NotificationSeverity.Warning, _service.HighestUnreadSeverity);
    }

    [Fact]
    public void MarkAllRead_ZeroesCountKeepsEntries()
    {
        _service.Post(NotificationSeverity.Warning, "Test", "A");
        _service.Post(NotificationSeverity.Error, "Test", "B");

        _service.MarkAllRead();

        Assert.Equal(0, _service.UnreadCount);
        Assert.Null(_service.HighestUnreadSeverity);
        Assert.Equal(2, _service.Notifications.Count);
    }

    [Fact]
    public void ClearAll_Empties()
    {
        _service.Post(NotificationSeverity.Warning, "Test", "A");
        _service.Post(NotificationSeverity.Error, "Test", "B");

        _service.ClearAll();

        Assert.Empty(_service.Notifications);
        Assert.Equal(0, _service.UnreadCount);
        Assert.Null(_service.HighestUnreadSeverity);
    }

    [Fact]
    public void Dismiss_RemovesOne()
    {
        _service.Post(NotificationSeverity.Warning, "Test", "A");
        _service.Post(NotificationSeverity.Error, "Test", "B");

        _service.Dismiss(_service.Notifications.Single(x => x.Title == "B"));

        var remaining = Assert.Single(_service.Notifications);
        Assert.Equal("A", remaining.Title);
        Assert.Equal(1, _service.UnreadCount);
        Assert.Equal(NotificationSeverity.Warning, _service.HighestUnreadSeverity);
    }

    [Fact]
    public async Task Post_FromBackgroundThread_AppliesOnlyThroughDispatch()
    {
        var queue = new ConcurrentQueue<Action>();
        var service = new NotificationService(NullLogger<NotificationService>.Instance, queue.Enqueue);

        await Task.Run(() => service.Post(NotificationSeverity.Warning, "Test", "Background"));

        Assert.Empty(service.Notifications);
        Assert.True(queue.TryDequeue(out var action));
        action();
        Assert.Equal("Background", Assert.Single(service.Notifications).Title);
        Assert.Equal(1, service.UnreadCount);
    }

    [Fact]
    public void Post_LogsAtSeverity()
    {
        var logger = new RecordingLogger();
        var service = new NotificationService(logger, a => a());

        service.Post(NotificationSeverity.Info, "Test", "I");
        service.Post(NotificationSeverity.Warning, "Test", "W", "detail");
        service.Post(NotificationSeverity.Error, "Test", "E");

        Assert.Equal([LogLevel.Information, LogLevel.Warning, LogLevel.Error], logger.Entries.Select(x => x.Level));
        Assert.Contains("detail", logger.Entries.ElementAt(1).Message);
    }

    private sealed class RecordingLogger : ILogger<NotificationService>
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
