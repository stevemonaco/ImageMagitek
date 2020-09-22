using Stylet;
using System.Windows.Media;

namespace TileShop.WPF.ViewModels
{
    public abstract class ToolViewModel : Screen
    {
        public abstract void SaveChanges();
        public abstract void DiscardChanges();

        private bool _isActive;
        public new bool IsActive
        {
            get => _isActive;
            set => SetAndNotify(ref _isActive, value);
        }

        private bool _isModified;
        public bool IsModified
        {
            get => _isModified;
            set => SetAndNotify(ref _isModified, value);
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetAndNotify(ref _isVisible, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndNotify(ref _isSelected, value);
        }

        private ImageSource _iconSource;
        public ImageSource IconSource
        {
            get => _iconSource;
            set => SetAndNotify(ref _iconSource, value);
        }

        private string _contentId;
        public string ContentId
        {
            get => _contentId;
            set => SetAndNotify(ref _contentId, value);
        }
    }
}
