using Stylet;
using System.Collections.Generic;
using System.Linq;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels
{
    public class ResourceRemovalChangesViewModel : Screen
    {
        private ResourceChangeViewModel _removedResource;
        public ResourceChangeViewModel RemovedResource
        {
            get => _removedResource;
            set => SetAndNotify(ref _removedResource, value);
        }

        private BindableCollection<ResourceChangeViewModel> _removedResources = new BindableCollection<ResourceChangeViewModel>();
        public BindableCollection<ResourceChangeViewModel> RemovedResources
        {
            get => _removedResources;
            set => SetAndNotify(ref _removedResources, value);
        }

        private BindableCollection<ResourceChangeViewModel> _changedResources = new BindableCollection<ResourceChangeViewModel>();
        public BindableCollection<ResourceChangeViewModel> ChangedResources
        {
            get => _changedResources;
            set => SetAndNotify(ref _changedResources, value);
        }

        private bool _hasRemovedResources;
        public bool HasRemovedResources
        {
            get => _hasRemovedResources;
            set => SetAndNotify(ref _hasRemovedResources, value);
        }

        private bool _hasChangedResources;
        public bool HasChangedResources
        {
            get => _hasChangedResources;
            set => SetAndNotify(ref _hasChangedResources, value);
        }

        public ResourceRemovalChangesViewModel(ResourceChangeViewModel removedResource)
        {
            RemovedResource = removedResource;
        }

        public ResourceRemovalChangesViewModel(ResourceChangeViewModel removedResource, IList<ResourceChangeViewModel> changes)
        {
            RemovedResource = removedResource;
            RemovedResources.AddRange(changes.Where(x => x.Removed));
            ChangedResources.AddRange(changes.Where(x => (x.LostElement || x.LostPalette) && !x.Removed));
            HasRemovedResources = RemovedResources.Any();
            HasChangedResources = ChangedResources.Any();
        }

        public void Remove() => RequestClose(true);
        public void Cancel() => RequestClose(false);
    }
}
