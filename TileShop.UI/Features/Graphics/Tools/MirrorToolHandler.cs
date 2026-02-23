using ImageMagitek;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class MirrorToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    private readonly MirrorOperation _operation;

    public MirrorToolHandler(MirrorOperation operation)
    {
        _operation = operation;
    }

    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (!ctx.MouseState.LeftButtonPressed)
            return ToolResult.Unhandled;

        var elementX = ctx.PixelX / state.WorkingArranger.ElementPixelSize.Width;
        var elementY = ctx.PixelY / state.WorkingArranger.ElementPixelSize.Height;

        var result = state.WorkingArranger.TryMirrorElement(elementX, elementY, _operation);
        if (result.HasSucceeded)
        {
            state.AddHistoryAction(new MirrorElementHistoryAction(elementX, elementY, _operation));
            state.IsModified = true;
            return ToolResult.HandledPixelData;
        }

        return ToolResult.HandledNoInvalidation;
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
