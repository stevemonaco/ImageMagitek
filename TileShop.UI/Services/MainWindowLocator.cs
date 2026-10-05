using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace TileShop.UI.Services;

internal static class MainWindowLocator
{
    public static Window? GetMainWindow() =>
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
