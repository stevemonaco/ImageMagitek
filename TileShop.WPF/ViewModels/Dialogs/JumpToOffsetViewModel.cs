using Stylet;

namespace TileShop.WPF.ViewModels
{
    public class JumpToOffsetViewModel : Screen
    {
        private long _offset;
        public long Offset
        {
            get => _offset;
            set => SetAndNotify(ref _offset, value);
        }

        public void Jump()
        {
            RequestClose(true);
        }

        public void Cancel() => RequestClose(false);
    }
}
