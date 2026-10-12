using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Features.Graphics.Tools;

public class PencilToolHandler : IToolHandler<GraphicsEditorViewModel>
{
    private Point _lastSample;

    public ToolCursor Cursor => ToolCursor.Crosshair;

    public ToolResult OnMouseDown(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (ctx.MouseState.LeftButtonPressed)
            return StartStroke(ctx, state, ColorPriority.Primary);

        if (ctx.MouseState.RightButtonPressed)
            return StartStroke(ctx, state, ColorPriority.Secondary);

        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseMove(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.IsPencilDrawing && ctx.MouseState.LeftButtonPressed)
            return ContinueStroke(ctx, state, ColorPriority.Primary);

        if (state.IsPencilDrawing && ctx.MouseState.RightButtonPressed)
            return ContinueStroke(ctx, state, ColorPriority.Secondary);

        state.UpdateActivityMessage(ctx.PixelX, ctx.PixelY);
        return ToolResult.Unhandled;
    }

    public ToolResult OnMouseUp(ToolContext ctx, GraphicsEditorViewModel state)
    {
        if (state.IsPencilDrawing && !ctx.MouseState.LeftButtonPressed && !ctx.MouseState.RightButtonPressed)
        {
            state.StopPencilDraw();
            return ToolResult.HandledNoInvalidation;
        }

        return ToolResult.Unhandled;
    }

    public ToolResult OnKeyDown(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;
    public ToolResult OnKeyUp(ToolContext ctx, GraphicsEditorViewModel state) => ToolResult.Unhandled;

    public Rectangle? GetTargetRect(ToolContext ctx, GraphicsEditorViewModel state) =>
        state.CanSetPixelAtPosition(ctx.PixelX, ctx.PixelY) ? new Rectangle(ctx.PixelX, ctx.PixelY, 1, 1) : null;

    public HistoryAction? Deactivate(GraphicsEditorViewModel state)
    {
        if (state.IsPencilDrawing)
            state.StopPencilDraw();

        return null;
    }

    private ToolResult StartStroke(ToolContext ctx, GraphicsEditorViewModel state, ColorPriority priority)
    {
        state.StartPencilDraw();
        var pixel = new Point((int)Math.Floor(ctx.X), (int)Math.Floor(ctx.Y));
        _lastSample = pixel;
        state.PaintPencilPixels([pixel], priority);
        return ToolResult.HandledNoInvalidation;
    }

    private ToolResult ContinueStroke(ToolContext ctx, GraphicsEditorViewModel state, ColorPriority priority)
    {
        var segment = new List<Point>();
        var last = _lastSample;
        foreach (var sample in ctx.Samples)
        {
            segment.AddRange(PixelLine.Points(last, sample).Skip(1));
            last = sample;
        }

        _lastSample = last;
        state.PaintPencilPixels(segment, priority);
        return ToolResult.HandledNoInvalidation;
    }
}
