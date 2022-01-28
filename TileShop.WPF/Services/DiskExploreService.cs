using System.Diagnostics;
using TileShop.Shared.Services;

namespace TileShop.WPF.Services;

public class DiskExploreService : IDiskExploreService
{
    public void ExploreDiskLocation(string location)
    {
        string command = $"explorer.exe";
        string args = $"/select, {location}";
        Process.Start(command, args);
    }
}
