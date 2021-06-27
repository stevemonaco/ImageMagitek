using Stylet;
using ImageMagitek.Project;

namespace TileShop.WPF.ViewModels
{
    public abstract class ResourceNodeViewModel : Screen
    {
        public ResourceNode Node { get; set; }
        public ResourceNodeViewModel ParentModel { get; set; }
        public abstract int SortPriority { get; }

        private BindableCollection<ResourceNodeViewModel> _children = new BindableCollection<ResourceNodeViewModel>();
        public BindableCollection<ResourceNodeViewModel> Children
        {
            get => _children;
            set => SetAndNotify(ref _children, value);
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetAndNotify(ref _isExpanded, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndNotify(ref _isSelected, value);
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotify(ref _name, value);
        }

        public void NotifyChildrenChanged()
        {
            NotifyOfPropertyChange(() => Children);
        }
    }
}
