using System.Diagnostics;
using System.Runtime.InteropServices;
using TileShop.Shared.Services;

namespace TileShop.AvaloniaUI.Services;

internal class DiskExploreService : IDiskExploreService
{
    public void ExploreDiskLocation(string location)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string command = $"explorer.exe";
            string args = $"/select, {location}";
            Process.Start(command, args);
        }
    }
}
