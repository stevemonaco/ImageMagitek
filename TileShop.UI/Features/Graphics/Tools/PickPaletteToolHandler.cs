using TileShop.Shared.Input;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class PickPaletteToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    public bool OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (ctx.MouseState.LeftButtonPressed && state.IsIndexedColor)
            return state.TryPickPalette(ctx.PixelX, ctx.PixelY);

        return false;
    }

    public bool OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return false;
    }

    public bool OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state) => false;
    public bool OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => false;
    public bool OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => false;
    public HistoryAction? Deactivate(GraphicsEditorViewModel state) => null;
}
