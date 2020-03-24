using Stylet;
using ImageMagitek.Project;
using Monaco.PathTree;
using System;
using System.Linq;
using InplaceEditBoxLib.Events;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Threading;

namespace TileShop.WPF.ViewModels
{
    public abstract class TreeNodeViewModel : Screen, InplaceEditBoxLib.Interfaces.IEditBox
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

        public event UserNotification.Events.ShowNotificationEventHandler ShowNotificationMessage;
        public event RequestEditEventHandler RequestEdit;
        public virtual bool RequestEditMode(RequestEditEvent request)
        {
            if (RequestEdit != null)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => RequestEdit(this, new RequestEdit(request))));
                return true;
            }

            return false;
        }
    }
}
