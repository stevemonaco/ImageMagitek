using System.Windows;
using Stylet;

namespace TileShop.WPF.ViewModels.Dialogs
{
    public class ResizeTiledScatteredArrangerViewModel : Screen
    {
        private IWindowManager _windowManager;

        private int _width;
        public int Width
        {
            get => _width;
            set => SetAndNotify(ref _width, value);
        }

        private int _height;
        public int Height
        {
            get => _height;
            set => SetAndNotify(ref _height, value);
        }

        private int _originalWidth;
        public int OriginalWidth
        {
            get => _originalWidth;
            set => SetAndNotify(ref _originalWidth, value);
        }

        private int _originalHeight;
        public int OriginalHeight
        {
            get => _originalHeight;
            set => SetAndNotify(ref _originalHeight, value);
        }

        /// <param name="originalWidth">Width of the original arranger in elements</param>
        /// <param name="originalHeight">Height of the original arranger in elements</param>
        public ResizeTiledScatteredArrangerViewModel(IWindowManager windowManager, int originalWidth, int originalHeight)
        {
            _windowManager = windowManager;

            OriginalWidth = originalWidth;
            OriginalHeight = originalHeight;
            Width = originalWidth;
            Height = originalHeight;
        }

        public void Resize()
        {
            if (Width < OriginalWidth || Height < OriginalHeight)
            {
                var result = _windowManager.ShowMessageBox("The specified dimensions will shrink the arranger. Elements outside of the new arranger dimensions will be lost. Continue?", buttons: MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    return;
            }
            RequestClose(true);
        }

        public void Cancel() => RequestClose(false);
    }
}
