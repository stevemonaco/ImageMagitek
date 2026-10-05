using System.Collections.Generic;

namespace TileShop.UI.ViewModels;

public sealed class ResourceNodeComparer : IComparer<ResourceNodeViewModel>
{
    public static ResourceNodeComparer Instance { get; } = new();

    public int Compare(ResourceNodeViewModel? x, ResourceNodeViewModel? y)
    {
        if (x is FolderNodeViewModel && y is FolderNodeViewModel)
            return string.Compare(x.Node.Name, y.Node.Name);
        else if (x is FolderNodeViewModel)
            return -1;
        else if (y is FolderNodeViewModel)
            return 1;
        else if (x is not null && y is not null)
            return string.Compare(x.Node.Name, y.Node.Name);
        else
            return 0;
    }
}
