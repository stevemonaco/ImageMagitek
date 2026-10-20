using System;
using System.Drawing;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;

namespace TileShop.UI.Features.Graphics.Tools;

/// <summary>
/// A cell plotting tool that acts on the rectangle of cells dragged from the press to the release
/// </summary>
public sealed class CellRectangleToolHandler<TState> : IToolHandler<TState> where TState : ICellPlotState
{
    private readonly Action<TState, Rectangle> _apply;
    private readonly Func<TState, Point, Rectangle?> _getHoverRect;
    private Point? _anchor;

    public CellRectangleToolHandler(Action<TState, Rectangle> apply, Func<TState, Point, Rectangle?>? getHoverRect = null)
    {
        _apply = apply;
        _getHoverRect = getHoverRect ?? ((state, cell) => state.CellsToPixels(new Rectangle(cell, new Size(1, 1))));
    }

    public ToolCursor Cursor => ToolCursor.Crosshair;

    public ToolResult OnMouseDown(ToolContext ctx, TState state)
    {
        if (!ctx.MouseState.LeftButtonPressed)
            return ToolResult.Unhandled;

        _anchor = state.CellAt(ctx.PixelX, ctx.PixelY);
        return ToolResult.HandledOverlay;
    }

    public ToolResult OnMouseMove(ToolContext ctx, TState state)
    {
        if (_anchor is not null && ctx.MouseState.LeftButtonPressed)
            return ToolResult.HandledNoInvalidation;

        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseUp(ToolContext ctx, TState state)
    {
        if (_anchor is not { } anchor || ctx.MouseState.LeftButtonPressed)
            return ToolResult.Unhandled;

        _anchor = null;
        _apply(state, CellRect.Between(anchor, state.CellAt(ctx.PixelX, ctx.PixelY)));
        return ToolResult.HandledOverlay;
    }

    public ToolResult OnKeyDown(ToolContext ctx, TState state) => ToolResult.Unhandled;
    public ToolResult OnKeyUp(ToolContext ctx, TState state) => ToolResult.Unhandled;

    public Rectangle? GetTargetRect(ToolContext ctx, TState state)
    {
        var cell = state.CellAt(ctx.PixelX, ctx.PixelY);
        return _anchor is { } anchor ? state.CellsToPixels(CellRect.Between(anchor, cell)) : _getHoverRect(state, cell);
    }

    public HistoryAction? Deactivate(TState state)
    {
        _anchor = null;
        return null;
    }
}
