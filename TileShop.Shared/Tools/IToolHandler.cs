using TileShop.Shared.Models;

namespace TileShop.Shared.Tools;

public readonly record struct ToolResult(bool Handled, InvalidationLevel Invalidation)
{
    public static ToolResult Unhandled => default;
    public static ToolResult HandledNoInvalidation => new(true, InvalidationLevel.None);
    public static ToolResult HandledOverlay => new(true, InvalidationLevel.Overlay);
    public static ToolResult HandledDisplay => new(true, InvalidationLevel.Display);
    public static ToolResult HandledPixelData => new(true, InvalidationLevel.PixelData);
}

public interface IToolHandler<in TState>
{
    ToolResult OnMouseDown(ToolContext ctx, TState state);
    ToolResult OnMouseMove(ToolContext ctx, TState state);
    ToolResult OnMouseUp(ToolContext ctx, TState state);
    ToolResult OnKeyDown(ToolContext ctx, TState state);
    ToolResult OnKeyUp(ToolContext ctx, TState state);

    /// <summary>
    /// Called when the tool is deactivated (switched away from or modifier released).
    /// The tool should finalize any in-progress operations and return a history action if applicable.
    /// </summary>
    HistoryAction? Deactivate(TState state);
}
