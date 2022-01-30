using System.Collections.Generic;

namespace TileShop.Shared.Services;

public interface IThemeService
{
    public void SetActiveTheme(string themeName);
    public string GetActiveTheme();
    public IEnumerable<string> GetAvailableThemes();
}
