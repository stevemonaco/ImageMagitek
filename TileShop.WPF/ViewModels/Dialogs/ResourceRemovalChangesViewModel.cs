using Stylet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TileShop.Shared.Models;

namespace TileShop.WPF.ViewModels.Dialogs
{
    public class ResourceRemovalChangesViewModel : Screen
    {
        private ResourceRemovalChange _removedItem;
        public ResourceRemovalChange RemovedItem
        {
            get => _removedItem;
            set => SetAndNotify(ref _removedItem, value);
        }

        private BindableCollection<ResourceRemovalChange> _removedItems;
        public BindableCollection<ResourceRemovalChange> RemovedItems
        {
            get => _removedItems;
            set => SetAndNotify(ref _removedItems, value);
        }

        private BindableCollection<ResourceRemovalChange> _changedItems;
        public BindableCollection<ResourceRemovalChange> ChangedItems
        {
            get => _changedItems;
            set => SetAndNotify(ref _changedItems, value);
        }

        private bool _hasChangedItems;
        public bool HasChangedItems
        {
            get => _hasChangedItems;
            set => SetAndNotify(ref _hasChangedItems, value);
        }

        public ResourceRemovalChangesViewModel(ResourceRemovalChange removedItem, IEnumerable<ResourceRemovalChange> changes)
        {
            RemovedItem = removedItem;
            RemovedItems = new BindableCollection<ResourceRemovalChange>(changes.Where(x => x.Removed == true));
            ChangedItems = new BindableCollection<ResourceRemovalChange>(changes.Where(x => x.Removed == false));
            HasChangedItems = ChangedItems.Count > 0 ? true : false;
        }

        public void Remove() => RequestClose(true);
        public void Cancel() => RequestClose(false);
    }
}
