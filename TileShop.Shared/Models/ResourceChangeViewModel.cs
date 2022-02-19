using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Project;

namespace TileShop.Shared.Models;

public partial class ResourceChangeViewModel : ObservableObject
{
    public IProjectResource Resource { get; }
    public ResourceNode ResourceNode { get; }

    [ObservableProperty] private string _resourceName;
    [ObservableProperty] private string _resourcePath;
    [ObservableProperty] private bool _removed;
    [ObservableProperty] private bool _lostPalette;
    [ObservableProperty] private bool _lostElement;
    [ObservableProperty] private bool _isChanged;

    public ResourceChangeViewModel(ResourceNode resourceNode, string resourcePathKey, bool removed, bool lostPalette, bool lostElement)
    {
        ResourceNode = resourceNode;
        Resource = ResourceNode.Item;
        ResourceName = Resource.Name;
        ResourcePath = resourcePathKey;
        Removed = removed;
        LostPalette = lostPalette;
        LostElement = lostElement;
    }

    public ResourceChangeViewModel(ResourceChange change)
    {
        Resource = change.Resource;
        ResourcePath = change.ResourcePath;
        ResourceNode = change.ResourceNode;
        Removed = change.Removed;
        LostPalette = change.LostPalette;
        LostElement = change.LostElement;
        IsChanged = change.IsChanged;
    }
}
