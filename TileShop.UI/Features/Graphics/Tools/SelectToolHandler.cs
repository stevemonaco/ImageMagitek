using TileShop.Shared.Input;
using TileShop.Shared.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class SelectToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    public bool OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.Selection.HasSelection && ctx.MouseState.LeftButtonPressed &&
            state.Selection.SelectionRect.ContainsPointSnapped(ctx.PixelX, ctx.PixelY))
        {
            // Start drag for selection (handled by DragDrop in View)
            return true;
        }

        if (state.Paste is not null && ctx.MouseState.LeftButtonPressed &&
            state.Paste.Rect.ContainsPointSnapped(ctx.PixelX, ctx.PixelY))
        {
            // Start drag for paste (handled by DragDrop in View)
            return true;
        }

        if (ctx.MouseState.LeftButtonPressed && ctx.MouseState.Modifiers.HasFlag(KeyModifiers.Control))
        {
            state.StartNewSelection(ctx.X, ctx.Y);
            state.CompleteSelection();
            return true;
        }

        if (ctx.MouseState.LeftButtonPressed)
        {
            state.StartNewSelection(ctx.X, ctx.Y);
            return true;
        }

        return false;
    }

    public bool OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.IsSelecting)
            state.UpdateSelection(ctx.X, ctx.Y);

        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return true;
    }

    public bool OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.IsSelecting && !ctx.MouseState.LeftButtonPressed)
        {
            state.CompleteSelection();
            return true;
        }

        return false;
    }

    public bool OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.EditMode == GraphicsEditMode.Arrange &&
            ctx.KeyState.Key == state.SecondaryAltKey && state.Paste is null)
        {
            if (state.TryStartNewSingleSelection(ctx.X, ctx.Y))
            {
                state.CompleteSelection();
                return true;
            }
        }

        return false;
    }

    public bool OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.EditMode == GraphicsEditMode.Arrange &&
            ctx.KeyState.Key == state.SecondaryAltKey && state.Paste is null &&
            state.WorkingArranger.ElementPixelSize == new System.Drawing.Size(
                state.Selection.SelectionRect.SnappedWidth,
                state.Selection.SelectionRect.SnappedHeight))
        {
            state.CancelOverlay();
            return true;
        }

        return false;
    }

    public HistoryAction? Deactivate(GraphicsEditorViewModel state)
    {
        if (state.IsSelecting)
            state.CompleteSelection();

        return null;
    }
}
