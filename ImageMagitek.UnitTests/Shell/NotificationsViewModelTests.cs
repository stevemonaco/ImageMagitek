using ImageMagitek.Services;
using Microsoft.Extensions.Logging.Abstractions;
using TileShop.Shared.Models;
using TileShop.Shared.Services;
using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Shell;

public class NotificationsViewModelTests
{
    private readonly NotificationService _service = new(NullLogger<NotificationService>.Instance, a => a());
    private readonly NotificationsViewModel _viewModel;

    public NotificationsViewModelTests()
    {
        _viewModel = new NotificationsViewModel(_service);
    }

    [Fact]
    public void IconSeverityAndCount_FollowPosts()
    {
        Assert.Null(_viewModel.IconSeverity);
        Assert.Equal(0, _viewModel.UnreadCount);

        _service.Post(NotificationSeverity.Info, "Test", "A");
        Assert.Equal(NotificationSeverity.Info, _viewModel.IconSeverity);

        _service.Post(NotificationSeverity.Warning, "Test", "B");
        Assert.Equal(NotificationSeverity.Warning, _viewModel.IconSeverity);
        Assert.Equal(2, _viewModel.UnreadCount);
    }

    [Fact]
    public void Open_ZeroesCountAndClearsFlash()
    {
        _service.Post(NotificationSeverity.Error, "Test", "A");

        _viewModel.OpenCommand.Execute(null);

        Assert.Equal(0, _viewModel.UnreadCount);
        Assert.Null(_viewModel.IconSeverity);
        Assert.False(_viewModel.IsFlashing);
        Assert.Single(_viewModel.Notifications);
    }

    [Fact]
    public void Post_NewError_SetsIsFlashing()
    {
        _service.Post(NotificationSeverity.Error, "Test", "A");

        Assert.True(_viewModel.IsFlashing);
    }

    [Fact]
    public void Post_WarningInfoOrCollapsedError_DoesNotFlash()
    {
        _service.Post(NotificationSeverity.Error, "Test", "A");
        _viewModel.OpenCommand.Execute(null);

        _service.Post(NotificationSeverity.Warning, "Test", "B");
        _service.Post(NotificationSeverity.Info, "Test", "C");
        _service.Post(NotificationSeverity.Error, "Test", "A");

        Assert.False(_viewModel.IsFlashing);
    }

    [Fact]
    public void StartupIssues_DoNotFlash()
    {
        StartupIssueNotifier.Post(_service, [new StartupIssue("Bad.xml", "Unexpected end of file")]);

        Assert.False(_viewModel.IsFlashing);
        Assert.Equal(NotificationSeverity.Warning, _viewModel.IconSeverity);
    }
}
