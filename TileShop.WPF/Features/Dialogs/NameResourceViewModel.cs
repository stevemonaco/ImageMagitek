using Stylet;

namespace TileShop.WPF.ViewModels
{
    public class NameResourceViewModel : Screen
    {
        private string _resourceName;
        public string ResourceName
        {
            get => _resourceName;
            set => SetAndNotify(ref _resourceName, value);
        }

        public void Confirm()
        {
            RequestClose(true);
        }

        public void Cancel() => RequestClose(false);
    }
}
