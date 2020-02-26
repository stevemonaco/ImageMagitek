using Stylet;
using ImageMagitek.Project;
using Monaco.PathTree;
using System;
using System.Linq;

namespace TileShop.Shared.ViewModels
{
    public abstract class TreeNodeViewModel : Screen
    {
        public IPathTreeNode<IProjectResource> Node { get; set; }
        public TreeNodeViewModel ParentModel { get; set; }
        public Type Type { get; protected set; }
        public abstract int SortPriority { get; }

        private BindableCollection<TreeNodeViewModel> _children = new BindableCollection<TreeNodeViewModel>();
        public BindableCollection<TreeNodeViewModel> Children
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

        private string _name;
        public string Name
        {
            get => _name;
            set => SetAndNotify(ref _name, value);
        }
    }
}
