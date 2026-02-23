using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class InspectElementToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;

    public ToolResult OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        state.InspectElementAtPosition(ctx.PixelX, ctx.PixelY);
        return ToolResult.HandledNoInvalidation;
    }

    public ToolResult OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public ToolResult OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public ToolResult OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public HistoryAction? Deactivate(GraphicsEditorViewModel state) => null;
}
