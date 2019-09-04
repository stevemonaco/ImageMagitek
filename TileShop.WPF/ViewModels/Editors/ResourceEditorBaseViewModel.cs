using Caliburn.Micro;
using ImageMagitek.Project;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.ViewModels
{
    public abstract class ResourceEditorBaseViewModel : EditorBaseViewModel
    {
        public override string Name => Resource?.Name;
        public IProjectResource Resource { get; protected set; }
    }
}
