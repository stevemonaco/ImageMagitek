using System;
using System.Linq;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Controls;
using Avalonia.Themes.Fluent;
using Avalonia.Markup.Xaml;
using FluentAvalonia.Styling;
using TileShop.Shared.Services;

namespace TileShop.AvaloniaUI.Services;

public sealed class ThemeService : IThemeService
{
    public ThemeStyle ActiveTheme { get; private set; }

    private FluentAvaloniaTheme _fluentAvaloniaService;
    private string _swappableTag = "SwappableResourceId";

    private ResourceDictionary[] _lightThemeResources = new[]
    {
        (ResourceDictionary) AvaloniaXamlLoader.Load(new Uri("avares://TileShop.AvaloniaUI/Styles/TileShop.Light.axaml"))
    };

    private ResourceDictionary[] _darkThemeResources = new[]
    {
        (ResourceDictionary) AvaloniaXamlLoader.Load(new Uri("avares://TileShop.AvaloniaUI/Styles/TileShop.Dark.axaml"))
    };

    public ThemeService()
    {
        _fluentAvaloniaService = AvaloniaLocator.Current!.GetService<FluentAvaloniaTheme>()!;

        if (_fluentAvaloniaService.RequestedTheme == FluentAvaloniaTheme.LightModeString)
            SetActiveTheme(ThemeStyle.Light);
        else if (_fluentAvaloniaService.RequestedTheme == FluentAvaloniaTheme.DarkModeString)
            SetActiveTheme(ThemeStyle.Dark);
    }

    public void SetActiveTheme(ThemeStyle themeStyle)
    {
        var styles = Application.Current!.Styles;
        var resources = Application.Current!.Resources;

        for (int i = 0; i < resources.MergedDictionaries.Count; i++)
        {
            if (resources.MergedDictionaries[i].TryGetResource(_swappableTag, out _))
            {
                resources.MergedDictionaries.RemoveAt(i);
                i--;
            }
        }

        var fluentTheme = styles.OfType<FluentTheme>().FirstOrDefault();

        if (themeStyle == ThemeStyle.Dark)
        {
            _fluentAvaloniaService.RequestedTheme = FluentAvaloniaTheme.DarkModeString;

            if (fluentTheme is not null)
                fluentTheme.Mode = FluentThemeMode.Dark;
            
            foreach (var resource in _darkThemeResources)
                resources.MergedDictionaries.Add(resource);
                
        }
        else if (themeStyle == ThemeStyle.Light)
        {
            _fluentAvaloniaService.RequestedTheme = FluentAvaloniaTheme.LightModeString;

            if (fluentTheme is not null)
                fluentTheme.Mode = FluentThemeMode.Light;

            foreach (var resource in _lightThemeResources)
                resources.MergedDictionaries.Add(resource);
        }

        ActiveTheme = themeStyle;
    }
}
