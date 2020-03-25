using ImageMagitek.Project;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.Shared.EventModels
{
    public class ResourceRenamedEvent
    {
        public IProjectResource Resource { get; }
        public string NewName { get; }
        public string OldName { get; }

        public ResourceRenamedEvent(IProjectResource resource, string newName, string oldName)
        {
            Resource = resource;
            NewName = newName;
            OldName = oldName;
        }
    }
}
