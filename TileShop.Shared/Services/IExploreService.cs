using System;

namespace TileShop.Shared.Services;

public interface IExploreService
{
    void ExploreDiskLocation(string location);
    void ExploreWebLocation(Uri location);
}
