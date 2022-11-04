using System.Collections.Generic;

namespace TileShop.Shared.Services;

public enum ThemeStyle { Light, Dark };

public interface IThemeService
{
    ThemeStyle ActiveTheme { get; }

    void SetActiveTheme(ThemeStyle themeStyle);
}
