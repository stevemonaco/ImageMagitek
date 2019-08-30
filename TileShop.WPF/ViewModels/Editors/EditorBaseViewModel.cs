using Caliburn.Micro;
using ImageMagitek.Project;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.ViewModels
{
    public abstract class EditorBaseViewModel : Screen
    {
        public string Name => Resource?.Name;
        public IProjectResource Resource { get; protected set; }

        protected bool _isModified;
        public bool IsModified
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
