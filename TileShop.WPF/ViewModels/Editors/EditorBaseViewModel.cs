using Stylet;

namespace TileShop.WPF.ViewModels
{
    public abstract class EditorBaseViewModel : Screen
    {
        public abstract string Name { get; }

        protected bool _isModified;
        public virtual bool IsModified
        {
            get => _isModified;
            set => SetAndNotify(ref _isModified, value);
        }
    }
}
