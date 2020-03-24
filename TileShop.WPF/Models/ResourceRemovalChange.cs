using ImageMagitek.Project;
using Monaco.PathTree;
using Stylet;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TileShop.WPF.ViewModels;

namespace TileShop.WPF.Models
{
    public class ResourceRemovalChange : INotifyPropertyChanged
    {
        public IProjectResource Resource { get; }
        public TreeNodeViewModel ModelNode { get; }
        public IPathTreeNode<IProjectResource> ResourceNode { get; }

        private string _resourceName;
        public string ResourceName
        {
            get => _resourceName;
            set => SetField(ref _resourceName, value);
        }

        private string _resourcePath;
        public string ResourcePath
        {
            get => _resourcePath;
            set => SetField(ref _resourcePath, value);
        }

        private bool _removed;
        public bool Removed
        {
            get => _removed;
            set => SetField(ref _removed, value);
        }

        private bool _lostPalette;
        public bool LostPalette
        {
            get => _lostPalette;
            set => SetField(ref _lostPalette, value);
        }

        private bool _lostElement;
        public bool LostElement
        {
            get => _lostElement;
            set => SetField(ref _lostElement, value);
        }

        public ResourceRemovalChange(TreeNodeViewModel modelNode, bool removed, bool lostPalette, bool lostElement)
        {
            ModelNode = modelNode;
            ResourceNode = modelNode.Node;
            Resource = ResourceNode.Value;
            ResourceName = Resource.Name;
            ResourcePath = ResourceNode.PathKey;
            Removed = removed;
            LostPalette = lostPalette;
            LostElement = lostElement;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
