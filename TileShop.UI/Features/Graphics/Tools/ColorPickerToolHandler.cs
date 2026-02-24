using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class ColorPickerToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (ctx.MouseState.LeftButtonPressed)
            return new ToolResult(state.PickColor(ctx.PixelX, ctx.PixelY, ColorPriority.Primary), InvalidationLevel.None);

        if (ctx.MouseState.RightButtonPressed)
            return new ToolResult(state.PickColor(ctx.PixelX, ctx.PixelY, ColorPriority.Secondary), InvalidationLevel.None);

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
