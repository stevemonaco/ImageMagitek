using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.Shared.EventModels
{
    public class ProjectLoadedEvent
    {
        public string ProjectFileName { get; set; }

        public ProjectLoadedEvent() { }

        public ProjectLoadedEvent(string projectFileName) 
        {
            ProjectFileName = projectFileName;
        }
    }
}
