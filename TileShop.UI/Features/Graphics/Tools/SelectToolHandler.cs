using TileShop.Shared.Input;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class SelectToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (ctx.MouseState.LeftButtonPressed && state.Selection.HasSelection)
        {
            var handle = state.HitTestHandle(ctx.X, ctx.Y);
            if (handle != SelectionHandle.None)
            {
                state.StartResize(handle);
                return ToolResult.HandledNoInvalidation;
            }
        }

        if (state.Selection.HasSelection && ctx.MouseState.LeftButtonPressed &&
            state.Selection.SelectionRect.ContainsPointSnapped(ctx.PixelX, ctx.PixelY))
        {
            // Start drag for selection (handled by DragDrop in View)
            return ToolResult.HandledNoInvalidation;
        }

        if (state.Paste is not null && ctx.MouseState.LeftButtonPressed &&
            state.Paste.Rect.ContainsPointSnapped(ctx.PixelX, ctx.PixelY))
        {
            // Start drag for paste (handled by DragDrop in View)
            return ToolResult.HandledNoInvalidation;
        }

        if (ctx.MouseState.LeftButtonPressed && ctx.MouseState.Modifiers.HasFlag(KeyModifiers.Control))
        {
            state.StartNewSelection(ctx.X, ctx.Y);
            state.CompleteSelection();
            return ToolResult.HandledOverlay;
        }

        if (ctx.MouseState.LeftButtonPressed)
        {
            state.StartNewSelection(ctx.X, ctx.Y);
            return ToolResult.HandledOverlay;
        }

        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.IsResizing)
        {
            state.UpdateResize(ctx.X, ctx.Y);
            return ToolResult.HandledNoInvalidation;
        }

        if (state.IsSelecting)
        {
            state.UpdateSelection(ctx.X, ctx.Y);
            return ToolResult.HandledNoInvalidation;
        }

        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return ToolResult.HandledNoInvalidation;
    }

    public ToolResult OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.IsResizing && !ctx.MouseState.LeftButtonPressed)
        {
            state.CompleteResize();
            return ToolResult.HandledNoInvalidation;
        }

        if (state.IsSelecting && !ctx.MouseState.LeftButtonPressed)
        {
            state.CompleteSelection();
            return ToolResult.HandledNoInvalidation;
        }

        return ToolResult.Unhandled;
    }

    public ToolResult OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.EditMode == GraphicsEditMode.Arrange &&
            ctx.KeyState.Key == state.SecondaryAltKey && state.Paste is null)
        {
            if (state.TryStartNewSingleSelection(ctx.X, ctx.Y))
            {
                state.CompleteSelection();
                return ToolResult.HandledNoInvalidation;
            }
        }

        return ToolResult.Unhandled;
    }

    public ToolResult OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.EditMode == GraphicsEditMode.Arrange &&
            ctx.KeyState.Key == state.SecondaryAltKey && state.Paste is null &&
            state.WorkingArranger.ElementPixelSize == new System.Drawing.Size(
                state.Selection.SelectionRect.SnappedWidth,
                state.Selection.SelectionRect.SnappedHeight))
        {
            state.CancelOverlay();
            return ToolResult.HandledNoInvalidation;
        }

        return ToolResult.Unhandled;
    }

    public HistoryAction? Deactivate(GraphicsEditorViewModel state)
    {
        if (state.IsResizing)
            state.CompleteResize();

        if (state.IsSelecting)
            state.CompleteSelection();

        return null;
    }
}
