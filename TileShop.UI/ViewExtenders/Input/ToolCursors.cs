using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TileShop.Shared.Tools;

namespace TileShop.UI.Input;

/// <summary>
/// Builds a precision crosshair cursor with an open center so the target pixel stays visible.
/// Rendered per display scaling because custom cursors are not DPI-scaled by the OS.
/// </summary>
public static class ToolCursors
{
    private const int _size = 20;
    private const int _crosshairCenter = 9;
    private const int _crosshairGap = 3;
    private const int _crosshairRadius = 7;

    private static readonly Dictionary<double, Cursor> _crosshairs = new();
    private static Cursor? _notAllowed;

    public static Cursor Get(ToolCursor cursor, double scaling) => cursor switch
    {
        ToolCursor.Crosshair => GetCrosshair(scaling),
        ToolCursor.NotAllowed => _notAllowed ??= new Cursor(StandardCursorType.No),
        _ => Cursor.Default,
    };

    private static Cursor GetCrosshair(double scaling)
    {
        if (!_crosshairs.TryGetValue(scaling, out var cursor))
        {
            cursor = CreateCrosshair(scaling);
            _crosshairs[scaling] = cursor;
        }

        return cursor;
    }

    private static Cursor CreateCrosshair(double scaling)
    {
        var size = (int)Math.Ceiling(_size * scaling);
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size));

        // Drawn on whole device pixels so the lines stay crisp at fractional scalings
        var center = (int)Math.Round(_crosshairCenter * scaling);
        var unit = Math.Max(1, (int)Math.Round(scaling));
        var gap = (int)Math.Round(_crosshairGap * scaling);
        var radius = (int)Math.Round(_crosshairRadius * scaling);
        var lineStart = center - unit / 2;

        using (var context = bitmap.CreateDrawingContext(true))
        {
            DrawArms(Brushes.White, gap - unit, radius + unit, lineStart - unit, 3 * unit);
            DrawArms(Brushes.Black, gap, radius, lineStart, unit);

            void DrawArms(IBrush brush, int start, int end, int lineOffset, int thickness)
            {
                var length = end - start + 1;
                context.FillRectangle(brush, new Rect(center + start, lineOffset, length, thickness));
                context.FillRectangle(brush, new Rect(center - end, lineOffset, length, thickness));
                context.FillRectangle(brush, new Rect(lineOffset, center + start, thickness, length));
                context.FillRectangle(brush, new Rect(lineOffset, center - end, thickness, length));
            }
        }

        return new Cursor(bitmap, new PixelPoint(center, center));
    }
}
