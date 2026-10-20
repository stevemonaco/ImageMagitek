using System;
using System.Drawing;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

/// <summary>
/// A Tiles mode tool that acts on the rectangle of cells dragged from the press to the release
/// </summary>
public sealed class CellRectangleToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    private readonly Action<GraphicsEditorViewModel, Rectangle> _apply;
    private readonly Func<GraphicsEditorViewModel, Point, Rectangle?> _getHoverRect;
    private Point? _anchor;

    public CellRectangleToolHandler(Action<GraphicsEditorViewModel, Rectangle> apply, Func<GraphicsEditorViewModel, Point, Rectangle?>? getHoverRect = null)
    {
        _apply = apply;
        _getHoverRect = getHoverRect ?? ((state, cell) => state.CellsToPixels(new Rectangle(cell, new Size(1, 1))));
    }

    public ToolCursor Cursor => ToolCursor.Crosshair;

    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (!ctx.MouseState.LeftButtonPressed)
            return ToolResult.Unhandled;

        _anchor = state.CellAt(ctx.PixelX, ctx.PixelY);
        return ToolResult.HandledOverlay;
    }

    public ToolResult OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (_anchor is not null && ctx.MouseState.LeftButtonPressed)
            return ToolResult.HandledNoInvalidation;

        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (_anchor is not { } anchor || ctx.MouseState.LeftButtonPressed)
            return ToolResult.Unhandled;

        _anchor = null;
        _apply(state, GraphicsEditorViewModel.CellRectangle(anchor, state.CellAt(ctx.PixelX, ctx.PixelY)));
        return ToolResult.HandledOverlay;
    }

    public ToolResult OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public ToolResult OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;

    public Rectangle? GetTargetRect(ToolContext ctx, GraphicsEditorViewModel state)
    {
        var cell = state.CellAt(ctx.PixelX, ctx.PixelY);
        return _anchor is { } anchor ? state.CellsToPixels(GraphicsEditorViewModel.CellRectangle(anchor, cell)) : _getHoverRect(state, cell);
    }

    public HistoryAction? Deactivate(GraphicsEditorViewModel state)
    {
        _anchor = null;
        return null;
    }
}
