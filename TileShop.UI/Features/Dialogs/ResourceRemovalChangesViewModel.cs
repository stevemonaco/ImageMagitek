using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TileShop.Shared.Interactions;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;
public partial class ResourceRemovalChangesViewModel : RequestViewModel<bool>
{
    [ObservableProperty] private ResourceChangeViewModel _removedResource;
    [ObservableProperty] private ObservableCollection<ResourceChangeViewModel> _removedResources = new();
    [ObservableProperty] private ObservableCollection<ResourceChangeViewModel> _changedResources = new();
    [ObservableProperty] private bool _hasRemovedResources;
    [ObservableProperty] private bool _hasChangedResources;

    public ResourceRemovalChangesViewModel(ResourceChangeViewModel removedResource)
    {
        _removedResource = removedResource;
        Title = "Resource Removal Changes";
    }

    public ResourceRemovalChangesViewModel(ResourceChangeViewModel removedResource, IList<ResourceChangeViewModel> changes)
    {
        _removedResource = removedResource;

        foreach (var removedItem in changes.Where(x => x.Removed))
            RemovedResources.Add(removedItem);

        foreach (var affectedItem in changes.Where(x => (x.LostElement || x.LostPalette) && !x.Removed))
            RemovedResources.Add(affectedItem);

        HasRemovedResources = RemovedResources.Any();
        HasChangedResources = ChangedResources.Any();
        Title = "Resource Removal Changes";
        AcceptName = "Remove";
    }

    public override bool ProduceResult() => true;
    //
    // protected override void Accept()
    // {
    //     Result = true;
    // }
    //
    // protected override Task<bool> TryCancel()
    // {
    //     Result = false;
    // }
}
