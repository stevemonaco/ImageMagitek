using System.Diagnostics;

namespace TileShop.WPF.Services;

public interface IDiskExploreService
{
    void ExploreDiskLocation(string location);
}

public class DiskExploreService : IDiskExploreService
{
    public void ExploreDiskLocation(string location)
    {
        string command = $"explorer.exe";
        string args = $"/select, {location}";
        Process.Start(command, args);
    }
}
