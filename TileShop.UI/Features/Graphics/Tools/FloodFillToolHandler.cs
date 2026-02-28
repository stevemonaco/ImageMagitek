using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class FloodFillToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (ctx.MouseState.LeftButtonPressed)
        {
            state.FloodFillAtPosition(ctx.PixelX, ctx.PixelY, ColorPriority.Primary);
            return ToolResult.HandledDisplay;
        }

        if (ctx.MouseState.RightButtonPressed)
        {
            state.FloodFillAtPosition(ctx.PixelX, ctx.PixelY, ColorPriority.Secondary);
            return ToolResult.HandledDisplay;
        }

        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public ToolResult OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public ToolResult OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public HistoryAction? Deactivate(GraphicsEditorViewModel state) => null;
}
