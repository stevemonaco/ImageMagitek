using System;
using System.Collections.Generic;
using System.Text;

namespace TileShop.Shared.EventModels
{
    public class NotifyOperationEvent
    {
        public string NotifyMessage { get; set; }

        public NotifyOperationEvent(string message)
        {
            NotifyMessage = message;
        }
    }
}
