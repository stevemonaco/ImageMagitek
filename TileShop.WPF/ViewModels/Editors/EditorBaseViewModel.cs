using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.ViewModels
{
    public abstract class EditorBaseViewModel : Screen
    {
        public abstract string Name { get; }

        protected bool _isModified;
        public virtual bool IsModified
        {
            get => _isModified;
            set
            {
                _isModified = value;
                NotifyOfPropertyChange(() => IsModified);
            }
        }
    }
}
