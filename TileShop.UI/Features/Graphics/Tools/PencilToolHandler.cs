using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class PencilToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (ctx.MouseState.LeftButtonPressed)
        {
            state.StartPencilDraw(ctx.PixelX, ctx.PixelY, ColorPriority.Primary);
            state.SetPixelAtPosition(ctx.PixelX, ctx.PixelY, ColorPriority.Primary);
            return ToolResult.HandledNoInvalidation;
        }

        if (ctx.MouseState.RightButtonPressed)
        {
            state.StartPencilDraw(ctx.PixelX, ctx.PixelY, ColorPriority.Secondary);
            state.SetPixelAtPosition(ctx.PixelX, ctx.PixelY, ColorPriority.Secondary);
            return ToolResult.HandledNoInvalidation;
        }

        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.IsPencilDrawing && ctx.MouseState.LeftButtonPressed)
        {
            state.SetPixelAtPosition(ctx.PixelX, ctx.PixelY, ColorPriority.Primary);
            return ToolResult.HandledNoInvalidation;
        }

        if (state.IsPencilDrawing && ctx.MouseState.RightButtonPressed)
        {
            state.SetPixelAtPosition(ctx.PixelX, ctx.PixelY, ColorPriority.Secondary);
            return ToolResult.HandledNoInvalidation;
        }

        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.IsPencilDrawing && !ctx.MouseState.LeftButtonPressed && !ctx.MouseState.RightButtonPressed)
        {
            state.StopPencilDraw();
            return ToolResult.HandledNoInvalidation;
        }

        return ToolResult.Unhandled;
    }

    public ToolResult OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public ToolResult OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;

    public HistoryAction? Deactivate(GraphicsEditorViewModel state)
    {
        if (state.IsPencilDrawing)
            state.StopPencilDraw();

        return null;
    }
}
