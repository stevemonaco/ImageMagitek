using System;
using System.Drawing;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

/// <summary>
/// A Tiles mode tool that acts on each cell the pointer drags across, recording the drag as one history step
/// </summary>
public sealed class TileStrokeToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    private readonly string _strokeName;
    private readonly Action<GraphicsEditorViewModel, Point> _plot;
    private readonly Func<GraphicsEditorViewModel, ToolContext, Rectangle?> _getTargetRect;

    public TileStrokeToolHandler(string strokeName, Action<GraphicsEditorViewModel, Point> plot,
        Func<GraphicsEditorViewModel, ToolContext, Rectangle?> getTargetRect)
    {
        _strokeName = strokeName;
        _plot = plot;
        _getTargetRect = getTargetRect;
    }

    public ToolCursor Cursor => ToolCursor.Crosshair;

    public Rectangle? GetTargetRect(ToolContext ctx, GraphicsEditorViewModel state) => _getTargetRect(state, ctx);

    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (!ctx.MouseState.LeftButtonPressed)
            return ToolResult.Unhandled;

        state.BeginTileStroke(_strokeName);
        _plot(state, state.CellAt(ctx.PixelX, ctx.PixelY));
        return ToolResult.HandledOverlay;
    }

    public ToolResult OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.IsTileStrokeActive && ctx.MouseState.LeftButtonPressed)
        {
            _plot(state, state.CellAt(ctx.PixelX, ctx.PixelY));
            return ToolResult.HandledNoInvalidation;
        }

        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (!state.IsTileStrokeActive || ctx.MouseState.LeftButtonPressed)
            return ToolResult.Unhandled;

        state.EndTileStroke();
        return ToolResult.HandledOverlay;
    }

    public ToolResult OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public ToolResult OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;

    public HistoryAction? Deactivate(GraphicsEditorViewModel state)
    {
        state.EndTileStroke();
        return null;
    }
}
