using Stylet;
using ImageMagitek.Project;
using Monaco.PathTree;
using System;

namespace TileShop.WPF.ViewModels
{
    public abstract class ProjectTreeNodeViewModel : Screen
    {
        public IPathTreeNode<IProjectResource> Node { get; set; }
        public Type Type { get; protected set; }

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
