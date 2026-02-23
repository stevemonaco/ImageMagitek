using TileShop.Shared.Input;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class InspectElementToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    public bool OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state) => false;

    public bool OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        state.InspectElementAtPosition(ctx.PixelX, ctx.PixelY);
        return true;
    }

    public bool OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state) => false;
    public bool OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => false;
    public bool OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => false;
    public HistoryAction? Deactivate(GraphicsEditorViewModel state) => null;
}
