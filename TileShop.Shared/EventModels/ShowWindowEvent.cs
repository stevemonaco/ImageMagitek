namespace TileShop.Shared.EventModels
{
    public enum ToolWindow { ProjectExplorer, PixelEditor }

    public record ShowToolWindowEvent(ToolWindow ToolWindow);
}
