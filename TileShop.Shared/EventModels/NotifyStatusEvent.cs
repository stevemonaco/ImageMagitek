﻿namespace TileShop.Shared.EventModels
{
    public enum NotifyStatusDuration { Short, Indefinite }
    public class NotifyStatusEvent
    {
        public string NotifyMessage { get; set; }

        public NotifyStatusDuration DisplayDuration { get; set; }

        public NotifyStatusEvent(string message, NotifyStatusDuration displayDuration)
        {
            NotifyMessage = message;
            DisplayDuration = displayDuration;
        }
    }
}
