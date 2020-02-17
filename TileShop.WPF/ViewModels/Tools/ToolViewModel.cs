using Stylet;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.ViewModels
{
    public abstract class ToolViewModel : Screen
    {
        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetAndNotify(ref _isVisible, value);
        }
    }
}
