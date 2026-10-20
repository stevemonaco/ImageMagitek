using System;
using System.Drawing;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;

namespace TileShop.UI.Features.Graphics.Tools;

/// <summary>
/// A cell plotting tool that acts on each cell the pointer drags across, recording the drag as one history step
/// </summary>
public sealed class TileStrokeToolHandler<TState> : IToolHandler<TState> where TState : ICellPlotState
{
    private readonly string _strokeName;
    private readonly Action<TState, Point> _plot;
    private readonly Func<TState, ToolContext, Rectangle?> _getTargetRect;

    public TileStrokeToolHandler(string strokeName, Action<TState, Point> plot, Func<TState, ToolContext, Rectangle?> getTargetRect)
    {
        _strokeName = strokeName;
        _plot = plot;
        _getTargetRect = getTargetRect;
    }

    public ToolCursor Cursor => ToolCursor.Crosshair;

    public Rectangle? GetTargetRect(ToolContext ctx, TState state) => _getTargetRect(state, ctx);

    public ToolResult OnMouseDown(ToolContext ctx, TState state)
    {
        if (!ctx.MouseState.LeftButtonPressed)
            return ToolResult.Unhandled;

        state.BeginCellStroke(_strokeName);
        _plot(state, state.CellAt(ctx.PixelX, ctx.PixelY));
        return ToolResult.HandledOverlay;
    }

    public ToolResult OnMouseMove(ToolContext ctx, TState state)
    {
        if (state.IsCellStrokeActive && ctx.MouseState.LeftButtonPressed)
        {
            _plot(state, state.CellAt(ctx.PixelX, ctx.PixelY));
            return ToolResult.HandledNoInvalidation;
        }

        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseUp(ToolContext ctx, TState state)
    {
        if (!state.IsCellStrokeActive || ctx.MouseState.LeftButtonPressed)
            return ToolResult.Unhandled;

        state.EndCellStroke();
        return ToolResult.HandledOverlay;
    }

    public ToolResult OnKeyDown(ToolContext ctx, TState state) => ToolResult.Unhandled;
    public ToolResult OnKeyUp(ToolContext ctx, TState state) => ToolResult.Unhandled;

    public HistoryAction? Deactivate(TState state)
    {
        state.EndCellStroke();
        return null;
    }
}
