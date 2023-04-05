using Avalonia;
using Avalonia.Styling;
using TileShop.Shared.Services;

namespace TileShop.UI.Services;

public sealed class ThemeService : IThemeService
{
    public ThemeStyle ActiveTheme { get; private set; }

    public ThemeService()
    {
        var themeKey = Application.Current!.RequestedThemeVariant!.Key;

        if (themeKey is "Default")
            SetActiveTheme(ThemeStyle.Light);
        else if (themeKey is "Dark")
            SetActiveTheme(ThemeStyle.Dark);
    }

    public void SetActiveTheme(ThemeStyle themeStyle)
    {
        var app = Application.Current!;

        if (themeStyle == ThemeStyle.Dark)
            app.RequestedThemeVariant = ThemeVariant.Dark;
        else if (themeStyle == ThemeStyle.Light)
            app.RequestedThemeVariant = ThemeVariant.Light;

        ActiveTheme = themeStyle;
    }
}
