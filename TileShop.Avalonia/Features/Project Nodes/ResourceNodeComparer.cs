using System.Collections.Generic;

namespace TileShop.AvaloniaUI.ViewModels;

class ResourceNodeComparer : IComparer<ResourceNodeViewModel>
{
    public int Compare(ResourceNodeViewModel x, ResourceNodeViewModel y)
    {
        if (x is FolderNodeViewModel && y is FolderNodeViewModel)
            return string.Compare(x.Node.Name, y.Node.Name);
        else if (x is FolderNodeViewModel)
            return -1;
        else if (y is FolderNodeViewModel)
            return 1;
        else
            return string.Compare(x.Node.Name, y.Node.Name);
    }
}
