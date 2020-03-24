using Stylet;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels.Dialogs
{
    public class ResourceRemovalChangesViewModel : Screen
    {
        private ResourceRemovalChange _removedResource;
        public ResourceRemovalChange RemovedResource
        {
            get => _removedResource;
            set => SetAndNotify(ref _removedResource, value);
        }

        private BindableCollection<ResourceRemovalChange> _removedResources = new BindableCollection<ResourceRemovalChange>();
        public BindableCollection<ResourceRemovalChange> RemovedResources
        {
            get => _removedResources;
            set => SetAndNotify(ref _removedResources, value);
        }

        private BindableCollection<ResourceRemovalChange> _changedResources = new BindableCollection<ResourceRemovalChange>();
        public BindableCollection<ResourceRemovalChange> ChangedResources
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

        public ResourceRemovalChangesViewModel(ResourceRemovalChange removedResource)
        {
            RemovedResource = removedResource;
        }

        public void Remove() => RequestClose(true);
        public void Cancel() => RequestClose(false);
    }
}
