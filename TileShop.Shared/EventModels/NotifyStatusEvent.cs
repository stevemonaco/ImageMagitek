namespace TileShop.Shared.EventModels;

public enum NotifyStatusDuration { Short, Indefinite, Reset }

public record NotifyStatusEvent(string NotifyMessage, NotifyStatusDuration DisplayDuration);
