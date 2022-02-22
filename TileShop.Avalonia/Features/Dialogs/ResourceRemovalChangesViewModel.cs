using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.AvaloniaUI.ViewExtenders;
using TileShop.Shared.Models;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class ResourceRemovalChangesViewModel : DialogViewModel<bool>
{
    [ObservableProperty] private ResourceChangeViewModel _removedResource;
    [ObservableProperty] private ObservableCollection<ResourceChangeViewModel> _removedResources = new();
    [ObservableProperty] private ObservableCollection<ResourceChangeViewModel> _changedResources = new();
    [ObservableProperty] private bool _hasRemovedResources;
    [ObservableProperty] private bool _hasChangedResources;

    public ResourceRemovalChangesViewModel(ResourceChangeViewModel removedResource)
    {
        RemovedResource = removedResource;
    }

    public ResourceRemovalChangesViewModel(ResourceChangeViewModel removedResource, IList<ResourceChangeViewModel> changes)
    {
        RemovedResource = removedResource;

        foreach (var removedItem in changes.Where(x => x.Removed))
            RemovedResources.Add(removedItem);

        foreach (var affectedItem in changes.Where(x => (x.LostElement || x.LostPalette) && !x.Removed))
            RemovedResources.Add(affectedItem);

        HasRemovedResources = RemovedResources.Any();
        HasChangedResources = ChangedResources.Any();
    }
}
