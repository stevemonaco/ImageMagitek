using ImageMagitek;
using TileShop.Shared.Input;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class RotateToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    private readonly RotationOperation _operation;

    public RotateToolHandler(RotationOperation operation)
    {
        _operation = operation;
    }

    public bool OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (!ctx.MouseState.LeftButtonPressed)
            return false;

        var elementX = ctx.PixelX / state.WorkingArranger.ElementPixelSize.Width;
        var elementY = ctx.PixelY / state.WorkingArranger.ElementPixelSize.Height;

        var result = state.WorkingArranger.TryRotateElement(elementX, elementY, _operation);
        if (result.HasSucceeded)
        {
            state.AddHistoryAction(new RotateElementHistoryAction(elementX, elementY, _operation));
            state.IsModified = true;
            state.Render();
        }

        return true;
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
