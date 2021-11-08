using ImageMagitek.Project;
using Stylet;

namespace TileShop.WPF.Models;

public class ResourceChangeViewModel : PropertyChangedBase
{
    public IProjectResource Resource { get; }
    public ResourceNode ResourceNode { get; }

    private string _resourceName;
    public string ResourceName
    {
        get => _resourceName;
        set => SetAndNotify(ref _resourceName, value);
    }

    private string _resourcePath;
    public string ResourcePath
    {
        get => _resourcePath;
        set => SetAndNotify(ref _resourcePath, value);
    }

    private bool _removed;
    public bool Removed
    {
        get => _removed;
        set => SetAndNotify(ref _removed, value);
    }

    private bool _lostPalette;
    public bool LostPalette
    {
        get => _lostPalette;
        set => SetAndNotify(ref _lostPalette, value);
    }

    private bool _lostElement;
    public bool LostElement
    {
        get => _lostElement;
        set => SetAndNotify(ref _lostElement, value);
    }

    private bool _isChanged;
    public bool IsChanged
    {
        get => _isChanged;
        set => SetAndNotify(ref _isChanged, value);
    }

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
