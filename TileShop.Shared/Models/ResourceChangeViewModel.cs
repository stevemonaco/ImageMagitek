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

        _resourceName = Resource.Name;
        _resourcePath = resourcePathKey;
        _removed = removed;
        _lostPalette = lostPalette;
        _lostElement = lostElement;
    }

    public ResourceChangeViewModel(ResourceChange change)
    {
        ResourceNode = change.ResourceNode;
        Resource = change.Resource;

        _resourceName = Resource.Name;
        _resourcePath = change.ResourcePath;
        _removed = change.Removed;
        _lostPalette = change.LostPalette;
        _lostElement = change.LostElement;
        IsChanged = change.IsChanged;
    }
}
