using System;
using System.Collections.Generic;
using System.Windows.Threading;
using ModernWpf;
using TileShop.Shared.Services;

namespace TileShop.WPF.Services;

public class ThemeService : IThemeService
{
    private const string LightThemeName = "Light";
    private const string DarkThemeName = "Dark";

    public string GetActiveTheme()
    {
        var theme = ThemeManager.Current.ActualApplicationTheme;

        if (theme == ApplicationTheme.Dark)
            return DarkThemeName;
        else if (theme == ApplicationTheme.Light)
            return LightThemeName;
        else
            throw new InvalidOperationException($"Active theme is unknown");
    }

    public IEnumerable<string> GetAvailableThemes() => new[] { "Dark", "Light" };

    public void SetActiveTheme(string themeName)
    {
        ApplicationTheme theme = themeName switch
        {
            LightThemeName => ApplicationTheme.Light,
            DarkThemeName => ApplicationTheme.Dark,
            _ => throw new InvalidOperationException($"Unknown theme '{themeName}'")
        };


        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            ThemeManager.Current.SetValue(ThemeManager.ApplicationThemeProperty, theme);
        });
    }
}
