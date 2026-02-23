using System;
using System.Collections.Generic;
using System.Drawing;
using ImageMagitek;
using ImageMagitek.Codec;
using TileShop.Shared.Input;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.Features.Graphics.Tools;

namespace TileShop.UI.ViewModels;

public partial class GraphicsEditorViewModel
{
    public Point? LastMousePosition { get; private set; }
    public Key PrimaryAltKey { get; private set; } = Key.LeftAlt;
    public Key SecondaryAltKey { get; private set; } = Key.LeftShift;

    private readonly Dictionary<ArrangerTool, IToolHandler<GraphicsEditorViewModel>> _arrangerTools = new()
    {
        [ArrangerTool.Select] = new SelectToolHandler(),
        [ArrangerTool.ApplyPalette] = new ApplyPaletteToolHandler(),
        [ArrangerTool.PickPalette] = new PickPaletteToolHandler(),
        [ArrangerTool.InspectElement] = new InspectElementToolHandler(),
        [ArrangerTool.RotateLeft] = new RotateToolHandler(RotationOperation.Left),
        [ArrangerTool.RotateRight] = new RotateToolHandler(RotationOperation.Right),
        [ArrangerTool.MirrorHorizontal] = new MirrorToolHandler(MirrorOperation.Horizontal),
        [ArrangerTool.MirrorVertical] = new MirrorToolHandler(MirrorOperation.Vertical),
    };

    private readonly Dictionary<ViewTool, IToolHandler<GraphicsEditorViewModel>> _viewTools = new()
    {
        [ViewTool.Select] = new SelectToolHandler(),
    };

    private readonly Dictionary<PixelTool, IToolHandler<GraphicsEditorViewModel>> _pixelTools = new()
    {
        [PixelTool.Select] = new SelectToolHandler(),
        [PixelTool.Pencil] = new PencilToolHandler(),
        [PixelTool.ColorPicker] = new ColorPickerToolHandler(),
        [PixelTool.FloodFill] = new FloodFillToolHandler(),
    };

    private IToolHandler<GraphicsEditorViewModel>? _modifierOverrideTool;

    private IToolHandler<GraphicsEditorViewModel>? ResolveActiveTool()
    {
        if (_modifierOverrideTool is not null)
            return _modifierOverrideTool;

        return EditMode switch
        {
            GraphicsEditMode.View => _viewTools[ActiveViewTool],
            GraphicsEditMode.Arrange => _arrangerTools[ActiveArrangerTool],
            GraphicsEditMode.Draw => _pixelTools[ActivePixelTool],
            _ => null
        };
    }

    private IToolHandler<GraphicsEditorViewModel>? ResolveToolWithModifiers(KeyModifiers modifiers)
    {
        if (_modifierOverrideTool is not null)
            return _modifierOverrideTool;

        if (EditMode == GraphicsEditMode.Draw && modifiers.HasFlag(KeyModifiers.Alt))
            return _pixelTools[PixelTool.ColorPicker];

        return ResolveActiveTool();
    }

    public bool MouseDown(double x, double y, MouseState mouseState)
    {
        var arranger = WorkingArranger;
        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);

        if (mouseState.LeftButtonPressed && Paste is not null && !Paste.Rect.ContainsPointSnapped(xc, yc))
        {
            ApplyPaste(Paste);
            Paste = null;
        }

        var ctx = new ToolContext(x, y, xc, yc, mouseState);
        var tool = ResolveToolWithModifiers(mouseState.Modifiers);

        var result = tool?.OnMouseDown(ctx, this) ?? default;
        if (result.Invalidation != InvalidationLevel.None)
            InvalidateEditor(result.Invalidation);
        return result.Handled;
    }

    public bool MouseUp(double x, double y, MouseState mouseState)
    {
        var arranger = WorkingArranger;
        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);

        var ctx = new ToolContext(x, y, xc, yc, mouseState);
        var tool = ResolveActiveTool();

        var result = tool?.OnMouseUp(ctx, this) ?? default;
        if (result.Invalidation != InvalidationLevel.None)
            InvalidateEditor(result.Invalidation);
        return result.Handled;
    }

    public bool MouseEnter()
    {
        return false;
    }

    public bool MouseLeave()
    {
        LastMousePosition = null;
        ActivityMessage = string.Empty;

        var tool = ResolveActiveTool();
        var historyAction = tool?.Deactivate(this);
        if (historyAction is not null)
            AddHistoryAction(historyAction);

        return true;
    }

    public bool MouseMove(double x, double y, MouseState mouseState)
    {
        var arranger = WorkingArranger;

        if (x < 0 || y < 0 || x >= arranger.ArrangerPixelSize.Width || y >= arranger.ArrangerPixelSize.Height)
        {
            LastMousePosition = null;
            return false;
        }

        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);

        LastMousePosition = new Point(xc, yc);

        // Shift+move in arranger mode for single-element selection
        if (EditMode == GraphicsEditMode.Arrange && mouseState.Modifiers.HasFlag(KeyModifiers.Shift) && Paste is null)
        {
            if (TryStartNewSingleSelection(x, y))
            {
                CompleteSelection();
                return true;
            }
        }

        var ctx = new ToolContext(x, y, xc, yc, mouseState);
        var tool = ResolveToolWithModifiers(mouseState.Modifiers);

        var result = tool?.OnMouseMove(ctx, this) ?? default;
        if (result.Invalidation != InvalidationLevel.None)
            InvalidateEditor(result.Invalidation);
        return result.Handled;
    }

    public bool MouseWheel(MouseWheelDirection direction, KeyModifiers modifiers)
    {
        if (EditMode != GraphicsEditMode.View)
            return false;

        if (direction == MouseWheelDirection.Down)
        {
            MovePageDown();
            return true;
        }

        if (direction == MouseWheelDirection.Up)
        {
            MovePageUp();
            return true;
        }

        return false;
    }

    public bool KeyPress(KeyState keyState, double? x, double? y)
    {
        if (!x.HasValue || !y.HasValue)
            return false;

        int xc = Math.Clamp((int)x.Value, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y.Value, 0, WorkingArranger.ArrangerPixelSize.Height - 1);

        // Handle modifier key override (e.g., Alt pushes ColorPicker in pixel mode)
        if (EditMode == GraphicsEditMode.Draw && keyState.Key == SecondaryAltKey && _modifierOverrideTool is null)
        {
            _modifierOverrideTool = _pixelTools[PixelTool.ColorPicker];
            return true;
        }

        var ctx = new ToolContext(x.Value, y.Value, xc, yc, keyState);
        var tool = ResolveActiveTool();

        var result = tool?.OnKeyDown(ctx, this) ?? default;
        if (result.Invalidation != InvalidationLevel.None)
            InvalidateEditor(result.Invalidation);
        return result.Handled;
    }

    public void KeyUp(KeyState keyState, double? x, double? y)
    {
        if (!x.HasValue || !y.HasValue)
            return;

        int xc = Math.Clamp((int)x.Value, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y.Value, 0, WorkingArranger.ArrangerPixelSize.Height - 1);

        // Release modifier override
        if (keyState.Key == SecondaryAltKey && _modifierOverrideTool is not null)
        {
            _modifierOverrideTool = null;
            return;
        }

        var ctx = new ToolContext(x.Value, y.Value, xc, yc, keyState);
        var tool = ResolveActiveTool();

        var result = tool?.OnKeyUp(ctx, this) ?? default;
        if (result.Invalidation != InvalidationLevel.None)
            InvalidateEditor(result.Invalidation);
    }

    internal void UpdateActivityMessage(int xc, int yc)
    {
        var arranger = WorkingArranger;

        if (Selection.HasSelection)
        {
            string notifyMessage;
            var rect = Selection.SelectionRect;
            if (rect.SnapMode == SnapMode.Element)
                notifyMessage = $"Element Selection: {rect.SnappedWidth / arranger.ElementPixelSize.Width} x {rect.SnappedHeight / arranger.ElementPixelSize.Height}" +
                    $" at ({rect.SnappedLeft / arranger.ElementPixelSize.Width}, {rect.SnappedTop / arranger.ElementPixelSize.Height})";
            else
                notifyMessage = $"Pixel Selection: {rect.SnappedWidth} x {rect.SnappedHeight} at ({rect.SnappedLeft}, {rect.SnappedTop})";

            ActivityMessage = notifyMessage;
        }
        else
        {
            var notifyMessage = $"{arranger.Name}: ({xc}, {yc})";
            ActivityMessage = notifyMessage;
        }
    }

    internal void InspectElementAtPosition(int xc, int yc)
    {
        var elX = xc / WorkingArranger.ElementPixelSize.Width;
        var elY = yc / WorkingArranger.ElementPixelSize.Height;
        var el = WorkingArranger.GetElement(elX, elY);

        if (el is { } element)
        {
            string paletteName = "Default";
            if (element.Codec is IIndexedCodec codec)
                paletteName = codec.Palette.Name;

            var sourceName = element.Source switch
            {
                FileDataSource fds => fds.FileLocation,
                MemoryDataSource => "Memory",
                _ => "None"
            };
            var fileOffsetDescription = $"0x{element.SourceAddress.ByteOffset:X}.{(element.SourceAddress.BitOffset != 0 ? element.SourceAddress.BitOffset.ToString() : "")}";

            ActivityMessage = IsIndexedColor
                ? $"Element ({elX}, {elY}): Codec {element.Codec.Name}, Palette {paletteName}, Source {sourceName}, FileOffset {fileOffsetDescription}"
                : $"Element ({elX}, {elY}): Codec {element.Codec.Name}, Source {sourceName}, FileOffset {fileOffsetDescription}";
        }
        else
        {
            ActivityMessage = $"Element ({elX}, {elY}): Empty";
        }
    }
}
