using System.Threading.Tasks;
using Avalonia.Input.Platform;

namespace TileShop.UI.Services;

/// <summary>
/// Text access to the OS clipboard through the main window
/// </summary>
public sealed class ClipboardService
{
    public async Task SetTextAsync(string text)
    {
        if (MainWindowLocator.GetMainWindow()?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    public async Task<string?> GetTextAsync()
    {
        if (MainWindowLocator.GetMainWindow()?.Clipboard is { } clipboard)
            return await clipboard.TryGetTextAsync();

        return null;
    }
}
