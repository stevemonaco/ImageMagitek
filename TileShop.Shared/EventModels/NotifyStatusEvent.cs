namespace TileShop.Shared.EventModels;

public enum NotifyStatusDuration { Short, Indefinite }

public record NotifyStatusEvent(string NotifyMessage, NotifyStatusDuration DisplayDuration);
