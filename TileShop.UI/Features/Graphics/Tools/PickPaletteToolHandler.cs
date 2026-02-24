using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class PickPaletteToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (ctx.MouseState.LeftButtonPressed && state.IsIndexedColor)
            return new ToolResult(state.TryPickPalette(ctx.PixelX, ctx.PixelY), InvalidationLevel.None);

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
