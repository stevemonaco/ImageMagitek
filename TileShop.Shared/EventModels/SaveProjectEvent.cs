using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.Shared.EventModels
{
    public class SaveProjectEvent
    {
        public bool SaveAsNewProject { get; set; }

        public SaveProjectEvent(bool saveAsNewProject)
        {
            SaveAsNewProject = saveAsNewProject;
        }
    }
}
