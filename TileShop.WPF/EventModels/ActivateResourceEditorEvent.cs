using ImageMagitek.Project;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.WPF.EventModels
{
    public class ActivateResourceEditorEvent
    {
        public IProjectResource Resource { get; set; }

        public ActivateResourceEditorEvent(IProjectResource resource)
        {
            Resource = resource;
        }
    }
}
