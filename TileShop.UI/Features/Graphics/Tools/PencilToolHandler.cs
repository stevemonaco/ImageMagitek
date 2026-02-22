using TileShop.Shared.Input;
using TileShop.Shared.Models;

namespace TileShop.UI.Features.Graphics.Tools;

public class PencilToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    public bool OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (ctx.MouseState.LeftButtonPressed)
        {
            state.StartPencilDraw(ctx.PixelX, ctx.PixelY, ColorPriority.Primary);
            state.SetPixelAtPosition(ctx.PixelX, ctx.PixelY, ColorPriority.Primary);
            return true;
        }

        if (ctx.MouseState.RightButtonPressed)
        {
            state.StartPencilDraw(ctx.PixelX, ctx.PixelY, ColorPriority.Secondary);
            state.SetPixelAtPosition(ctx.PixelX, ctx.PixelY, ColorPriority.Secondary);
            return true;
        }

        return false;
    }

    public bool OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.IsPencilDrawing && ctx.MouseState.LeftButtonPressed)
        {
            state.SetPixelAtPosition(ctx.PixelX, ctx.PixelY, ColorPriority.Primary);
            return true;
        }

        if (state.IsPencilDrawing && ctx.MouseState.RightButtonPressed)
        {
            state.SetPixelAtPosition(ctx.PixelX, ctx.PixelY, ColorPriority.Secondary);
            return true;
        }

        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return false;
    }

    public bool OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.IsPencilDrawing && !ctx.MouseState.LeftButtonPressed && !ctx.MouseState.RightButtonPressed)
        {
            state.StopPencilDraw();
            return true;
        }

        return false;
    }

    public bool OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => false;
    public bool OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => false;

    public HistoryAction? Deactivate(GraphicsEditorViewModel state)
    {
        if (state.IsPencilDrawing)
            state.StopPencilDraw();

        return null;
    }
}
