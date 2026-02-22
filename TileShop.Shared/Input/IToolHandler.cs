using TileShop.Shared.Models;

namespace TileShop.Shared.Input;

public interface IToolHandler<in TState>
{
    bool OnMouseDown(ToolContext ctx, TState state);
    bool OnMouseMove(ToolContext ctx, TState state);
    bool OnMouseUp(ToolContext ctx, TState state);
    bool OnKeyDown(ToolContext ctx, TState state);
    bool OnKeyUp(ToolContext ctx, TState state);

    /// <summary>
    /// Called when the tool is deactivated (switched away from or modifier released).
    /// The tool should finalize any in-progress operations and return a history action if applicable.
    /// </summary>
    HistoryAction? Deactivate(TState state);
}
