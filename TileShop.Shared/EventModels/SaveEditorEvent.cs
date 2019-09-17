using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.Shared.EventModels
{
    public class SaveEditorEvent
    {
        public bool PromptUser { get; set; }

        public SaveEditorEvent() { }

        public SaveEditorEvent(bool promptUser)
        {
            PromptUser = promptUser;
        }
    }
}
