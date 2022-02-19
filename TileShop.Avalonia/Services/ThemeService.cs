using System;
using System.Collections.Generic;
using TileShop.Shared.Services;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Markup.Xaml.Styling;

namespace TileShop.AvaloniaUI.Services;

public class ThemeService : IThemeService
{
    //private Theme? _selectedTheme;
    //private IList<Theme>? _themes;
    //private IList<Window>? _windows;

    private const string FluentLightThemeName = "Fluent Light";
    private const string FluentDarkThemeName = "Fluent Dark";
    private const string DefaultLightThemeName = "Default Light";
    private const string DefaultDarkThemeName = "Default Dark";

    public string GetActiveTheme()
    {
        var theme = Application.Current.Styles[0];

        if (theme == FluentLight)
            return FluentLightThemeName;
        else if (theme == FluentDark)
            return FluentDarkThemeName;
        else if (theme == DefaultLight)
            return DefaultLightThemeName;
        else if (theme == DefaultDark)
            return DefaultDarkThemeName;
        else
            throw new InvalidOperationException($"Unknown theme");
    }

    public IEnumerable<string> GetAvailableThemes() => new[] { FluentLightThemeName, FluentDarkThemeName, DefaultLightThemeName, DefaultDarkThemeName };

    public void SetActiveTheme(string themeName)
    {
        var theme = themeName switch
        {
            FluentLightThemeName => FluentLight,
            FluentDarkThemeName => FluentDark,
            DefaultLightThemeName => DefaultLight,
            DefaultDarkThemeName => DefaultDark,
            _ => throw new ArgumentException($"Unsupported theme '{themeName}'")
        };

        Application.Current!.Styles[0] = theme;
    }

    public static Styles DefaultLight = new Styles
    {
        new StyleInclude(new Uri("avares://Avalonia.ThemeManager/Styles"))
        {
            Source = new Uri("avares://Avalonia.Themes.Default/DefaultTheme.xaml")
        },
        new StyleInclude(new Uri("avares://Avalonia.ThemeManager/Styles"))
        {
            Source = new Uri("avares://Avalonia.Themes.Default/Accents/BaseLight.xaml")
        }
    };

    public static Styles DefaultDark = new Styles
    {
        new StyleInclude(new Uri("avares://Avalonia.ThemeManager/Styles"))
        {
            Source = new Uri("avares://Avalonia.Themes.Default/DefaultTheme.xaml")
        },
        new StyleInclude(new Uri("avares://Avalonia.ThemeManager/Styles"))
        {
            Source = new Uri("avares://Avalonia.Themes.Default/Accents/BaseDark.xaml")
        }
    };

    public static Styles FluentLight = new Styles
    {
        new StyleInclude(new Uri("avares://Avalonia.ThemeManager/Styles"))
        {
            Source = new Uri("avares://Avalonia.Themes.Fluent/FluentLight.xaml")
        }
    };

    public static Styles FluentDark = new Styles
    {
        new StyleInclude(new Uri("avares://Avalonia.ThemeManager/Styles"))
        {
            Source = new Uri("avares://Avalonia.Themes.Fluent/FluentDark.xaml")
        }
    };
}
