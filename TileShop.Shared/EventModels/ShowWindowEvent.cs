using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.Shared.EventModels
{
    public enum ToolWindow { ProjectExplorer, PixelEditor }
    public class ShowToolWindowEvent
    {
        public ToolWindow ToolWindow { get; set; }

        public ShowToolWindowEvent(ToolWindow toolWindow)
        {
            ToolWindow = toolWindow;
        }
    }
}
