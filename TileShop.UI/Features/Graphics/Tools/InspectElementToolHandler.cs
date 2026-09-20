using System.Drawing;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class InspectElementToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    public ToolCursor Cursor => ToolCursor.Crosshair;

    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;

    public ToolResult OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        state.InspectElementAtPosition(ctx.PixelX, ctx.PixelY);
        return ToolResult.HandledNoInvalidation;
    }

    public ToolResult OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public ToolResult OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public ToolResult OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;

    public Rectangle? GetTargetRect(ToolContext ctx, GraphicsEditorViewModel state) =>
        state.GetElementRectAtPixel(ctx.PixelX, ctx.PixelY);

    public HistoryAction? Deactivate(GraphicsEditorViewModel state) => null;
}
