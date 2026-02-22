using System;
using System.Drawing;
using ImageMagitek;
using ImageMagitek.Codec;
using TileShop.Shared.Input;
using TileShop.Shared.Models;

namespace TileShop.UI.Features.Graphics;

public partial class GraphicsEditorViewModel
{
    public Point? LastMousePosition { get; private set; }
    public Key PrimaryAltKey { get; private set; } = Key.LeftAlt;
    public Key SecondaryAltKey { get; private set; } = Key.LeftShift;
    
    public bool MouseDown(double x, double y, MouseState mouseState)
    {
        bool isHandled = false;
        var arranger = WorkingArranger;

        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);

        if (mouseState.LeftButtonPressed && Paste is not null && !Paste.Rect.ContainsPointSnapped(xc, yc))
        {
            ApplyPaste(Paste);
            Paste = null;
        }

        if (EditMode == GraphicsEditMode.Draw)
        {
            isHandled = MouseDownPixelMode(x, y, xc, yc, mouseState);
        }
        else if (EditMode == GraphicsEditMode.Arrange)
        {
            isHandled = MouseDownArrangerMode(x, y, xc, yc, mouseState);
        }

        return isHandled;
    }

    private bool MouseDownArrangerMode(double x, double y, int xc, int yc, MouseState mouseState)
    {
        var elementX = xc / WorkingArranger.ElementPixelSize.Width;
        var elementY = yc / WorkingArranger.ElementPixelSize.Height;

        if (ActiveArrangerTool == ArrangerTool.ApplyPalette && mouseState.LeftButtonPressed && SelectedPalette is not null && IsIndexedColor)
        {
            _applyPaletteHistory = new ApplyPaletteHistoryAction(SelectedPalette.Palette);
            TryApplyPalette(xc, yc, SelectedPalette.Palette);
            return true;
        }

        if (ActiveArrangerTool == ArrangerTool.PickPalette && mouseState.LeftButtonPressed && IsIndexedColor)
        {
            return TryPickPalette(xc, yc);
        }

        if (ActiveArrangerTool == ArrangerTool.RotateLeft && mouseState.LeftButtonPressed)
        {
            var result = WorkingArranger.TryRotateElement(elementX, elementY, RotationOperation.Left);
            if (result.HasSucceeded)
            {
                AddHistoryAction(new RotateElementHistoryAction(elementX, elementY, RotationOperation.Left));
                IsModified = true;
                Render();
            }

            return true;
        }

        if (ActiveArrangerTool == ArrangerTool.RotateRight && mouseState.LeftButtonPressed)
        {
            var result = WorkingArranger.TryRotateElement(elementX, elementY, RotationOperation.Right);
            if (result.HasSucceeded)
            {
                AddHistoryAction(new RotateElementHistoryAction(elementX, elementY, RotationOperation.Right));
                IsModified = true;
                Render();
            }
            return true;
        }

        if (ActiveArrangerTool == ArrangerTool.MirrorHorizontal && mouseState.LeftButtonPressed)
        {
            var result = WorkingArranger.TryMirrorElement(elementX, elementY, MirrorOperation.Horizontal);
            if (result.HasSucceeded)
            {
                AddHistoryAction(new MirrorElementHistoryAction(elementX, elementY, MirrorOperation.Horizontal));
                IsModified = true;
                Render();
            }
            return true;
        }

        if (ActiveArrangerTool == ArrangerTool.MirrorVertical && mouseState.LeftButtonPressed)
        {
            var result = WorkingArranger.TryMirrorElement(elementX, elementY, MirrorOperation.Vertical);
            if (result.HasSucceeded)
            {
                AddHistoryAction(new MirrorElementHistoryAction(elementX, elementY, MirrorOperation.Vertical));
                IsModified = true;
                Render();
            }
            return true;
        }

        if (ActiveArrangerTool == ArrangerTool.Select)
        {
            return MouseDownSelectMode(x, y, xc, yc, mouseState);
        }

        return false;
    }

    private bool MouseDownSelectMode(double x, double y, int xc, int yc, MouseState mouseState)
    {
        if (Selection.HasSelection && mouseState.LeftButtonPressed && Selection.SelectionRect.ContainsPointSnapped(xc, yc))
        {
            // Start drag for selection (Handled by DragDrop in View)
        }
        else if (Paste is not null && mouseState.LeftButtonPressed && Paste.Rect.ContainsPointSnapped(xc, yc))
        {
            // Start drag for paste (Handled by DragDrop in View)
        }
        else if (mouseState.LeftButtonPressed && mouseState.Modifiers.HasFlag(KeyModifiers.Control))
        {
            IsSelecting = true;
            StartNewSelection(x, y);
            CompleteSelection();
        }
        else if (mouseState.LeftButtonPressed)
        {
            IsSelecting = true;
            StartNewSelection(x, y);
        }
        else
        {
            return false;
        }

        return true;
    }

    private bool MouseDownPixelMode(double x, double y, int xc, int yc, MouseState mouseState)
    {
        if ((ActivePixelTool == PixelTool.ColorPicker || mouseState.Modifiers.HasFlag(KeyModifiers.Alt)) && mouseState.LeftButtonPressed)
        {
            return PickColor(xc, yc, ColorPriority.Primary);
        }
        
        if ((ActivePixelTool == PixelTool.ColorPicker || mouseState.Modifiers.HasFlag(KeyModifiers.Alt)) && mouseState.RightButtonPressed)
        {
            return PickColor(xc, yc, ColorPriority.Secondary);
        }
        
        if (ActivePixelTool == PixelTool.Pencil && mouseState.LeftButtonPressed)
        {
            StartPencilDraw(xc, yc, ColorPriority.Primary);
            SetPixelAtPosition(xc, yc, ColorPriority.Primary);
            return true;
        }
        
        if (ActivePixelTool == PixelTool.Pencil && mouseState.RightButtonPressed)
        {
            StartPencilDraw(xc, yc, ColorPriority.Secondary);
            SetPixelAtPosition(xc, yc, ColorPriority.Secondary);
            return true;
        }
        
        if (ActivePixelTool == PixelTool.FloodFill && mouseState.LeftButtonPressed)
        {
            FloodFillAtPosition(xc, yc, ColorPriority.Primary);
            return true;
        }
        
        if (ActivePixelTool == PixelTool.FloodFill && mouseState.RightButtonPressed)
        {
            FloodFillAtPosition(xc, yc, ColorPriority.Secondary);
            return true;
        }

        return MouseDownSelectMode(x, y, xc, yc, mouseState);
    }

    public bool MouseUp(double x, double y, MouseState mouseState)
    {
        if (EditMode == GraphicsEditMode.Arrange && ActiveArrangerTool == ArrangerTool.ApplyPalette && _applyPaletteHistory?.ModifiedElements.Count > 0)
        {
            AddHistoryAction(_applyPaletteHistory);
            _applyPaletteHistory = null;
        }
        else if (EditMode == GraphicsEditMode.Draw && IsPencilDrawing && !mouseState.LeftButtonPressed && !mouseState.RightButtonPressed)
        {
            StopPencilDraw();
        }
        else if (IsSelecting && mouseState.LeftButtonPressed == false)
        {
            CompleteSelection();
        }

        return true;
    }

    public bool MouseEnter()
    {
        return false;
    }

    public bool MouseLeave()
    {
        LastMousePosition = null;
        ActivityMessage = string.Empty;

        if (EditMode == GraphicsEditMode.Arrange && ActiveArrangerTool == ArrangerTool.ApplyPalette && _applyPaletteHistory?.ModifiedElements.Count > 0)
        {
            AddHistoryAction(_applyPaletteHistory);
            _applyPaletteHistory = null;
        }
        else if (EditMode == GraphicsEditMode.Draw)
        {
            if (ActivePixelTool == PixelTool.Pencil && IsPencilDrawing)
            {
                StopPencilDraw();
            }
            else
            {
                PopPixelTool();
            }
        }

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

        if (EditMode == GraphicsEditMode.Draw)
        {
            MouseMovePixelMode(x, y, xc, yc, mouseState);
            return true;
        }
        else if (EditMode == GraphicsEditMode.Arrange)
        {
            MouseMoveArrangerMode(x, y, xc, yc, mouseState);
            return true;
        }

        return false;
    }

    private void MouseMoveArrangerMode(double x, double y, int xc, int yc, MouseState mouseState)
    {
        if (mouseState.Modifiers.HasFlag(KeyModifiers.Shift) && Paste is null)
        {
            if (TryStartNewSingleSelection(x, y))
            {
                CompleteSelection();
                return;
            }
        }

        if (ActiveArrangerTool == ArrangerTool.ApplyPalette && mouseState.LeftButtonPressed && _applyPaletteHistory is not null && SelectedPalette is not null && IsIndexedColor)
        {
            TryApplyPalette(xc, yc, SelectedPalette.Palette);
        }
        else if (ActiveArrangerTool == ArrangerTool.InspectElement)
        {
            InspectElementAtPosition(xc, yc);
        }
        else if (ActiveArrangerTool == ArrangerTool.Select)
        {
            if (IsSelecting)
                UpdateSelection(x, y);
            UpdateActivityMessage(xc, yc);
        }
        else
        {
            UpdateActivityMessage(xc, yc);
        }
    }

    private void MouseMovePixelMode(double x, double y, int xc, int yc, MouseState mouseState)
    {
        if (x < 0 || x >= WorkingArranger.ArrangerPixelSize.Width || y < 0 || y >= WorkingArranger.ArrangerPixelSize.Height)
        {
            LastMousePosition = null;
            return;
        }

        if (IsPencilDrawing && ActivePixelTool == PixelTool.Pencil && mouseState.LeftButtonPressed)
            SetPixelAtPosition(xc, yc, ColorPriority.Primary);
        else if (IsPencilDrawing && ActivePixelTool == PixelTool.Pencil && mouseState.RightButtonPressed)
            SetPixelAtPosition(xc, yc, ColorPriority.Secondary);
        else
        {
            if (IsSelecting)
                UpdateSelection(x, y);
            UpdateActivityMessage(xc, yc);
        }
    }

    private void InspectElementAtPosition(int xc, int yc)
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

    private void UpdateActivityMessage(int xc, int yc)
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

    public bool MouseWheel(MouseWheelDirection direction, KeyModifiers modifiers)
    {
        return false;
    }

    public bool KeyPress(KeyState keyState, double? x, double? y)
    {
        if (EditMode == GraphicsEditMode.Arrange)
        {
            if (keyState.Key == SecondaryAltKey && x.HasValue && y.HasValue && Paste is null)
            {
                if (TryStartNewSingleSelection(x.Value, y.Value))
                {
                    CompleteSelection();
                    return true;
                }
            }
        }
        else if (EditMode == GraphicsEditMode.Draw)
        {
            if (keyState.Key == SecondaryAltKey && x.HasValue && y.HasValue && Paste is null && _priorPixelTool is null)
            {
                PushPixelTool(PixelTool.ColorPicker);
                return true;
            }
        }

        return false;
    }

    public void KeyUp(KeyState keyState, double? x, double? y)
    {
        if (EditMode == GraphicsEditMode.Arrange)
        {
            if (keyState.Key == SecondaryAltKey && x.HasValue && y.HasValue && Paste is null &&
                WorkingArranger.ElementPixelSize == new Size(Selection.SelectionRect.SnappedWidth, Selection.SelectionRect.SnappedHeight))
            {
                CancelOverlay();
                return;
            }
        }
        else if (EditMode == GraphicsEditMode.Draw)
        {
            if (keyState.Key == SecondaryAltKey && x.HasValue && y.HasValue && Paste is null)
            {
                PopPixelTool();
            }
        }
    }
}
