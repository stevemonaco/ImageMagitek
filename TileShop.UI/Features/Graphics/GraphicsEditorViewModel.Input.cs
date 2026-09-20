using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Avalonia.Media;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using TileShop.Shared.Input;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.Features.Graphics.Tools;

namespace TileShop.UI.ViewModels;

public partial class GraphicsEditorViewModel
{
    public Point? LastMousePosition { get; private set; }

    /// <summary>Pixel region the active tool would act on at the pointer position, or null if none.</summary>
    public Rectangle? HoverTarget { get; private set; }

    public IReadOnlyList<Key> AlternativeToolKeys { get; } = [Key.LeftControl, Key.RightControl];
    public IReadOnlyList<Key> TertiaryToolKeys { get; } = [Key.LeftShift, Key.RightShift];

    private readonly Dictionary<ArrangeTool, IToolHandler<GraphicsEditorViewModel>> _arrangerTools = new()
    {
        [ArrangeTool.ElementSelect] = new ElementSelectToolHandler(),
        [ArrangeTool.PixelSelect] = new PixelSelectToolHandler(),
        [ArrangeTool.ApplyPalette] = new ApplyPaletteToolHandler(),
        [ArrangeTool.PickPalette] = new PickPaletteToolHandler(),
        [ArrangeTool.InspectElement] = new InspectElementToolHandler(),
        [ArrangeTool.RotateLeft] = new RotateToolHandler(RotationOperation.Left),
        [ArrangeTool.RotateRight] = new RotateToolHandler(RotationOperation.Right),
        [ArrangeTool.MirrorHorizontal] = new MirrorToolHandler(MirrorOperation.Horizontal),
        [ArrangeTool.MirrorVertical] = new MirrorToolHandler(MirrorOperation.Vertical),
    };

    private readonly Dictionary<ViewTool, IToolHandler<GraphicsEditorViewModel>> _viewTools = new()
    {
        [ViewTool.ElementSelect] = new ElementSelectToolHandler(),
        [ViewTool.PixelSelect] = new PixelSelectToolHandler(),
    };

    private readonly Dictionary<DrawTool, IToolHandler<GraphicsEditorViewModel>> _pixelTools = new()
    {
        [DrawTool.PixelSelect] = new PixelSelectToolHandler(),
        [DrawTool.Pencil] = new PencilToolHandler(),
        [DrawTool.ColorPicker] = new ColorPickerToolHandler(),
        [DrawTool.FloodFill] = new FloodFillToolHandler(),
    };

    private IToolHandler<GraphicsEditorViewModel>? _modifierOverrideTool;

    private IToolHandler<GraphicsEditorViewModel>? ResolveActiveTool()
    {
        if (_modifierOverrideTool is not null)
            return _modifierOverrideTool;

        return EditMode switch
        {
            GraphicsEditMode.View => _viewTools[SelectedViewTool],
            GraphicsEditMode.Arrange => _arrangerTools[SelectedArrangeTool],
            GraphicsEditMode.Draw => _pixelTools[SelectedDrawTool],
            _ => null
        };
    }

    private IToolHandler<GraphicsEditorViewModel>? ResolveToolWithModifiers(KeyModifiers modifiers)
    {
        if (_modifierOverrideTool is not null)
            return _modifierOverrideTool;

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            if (EditMode == GraphicsEditMode.Draw)
                return _pixelTools[DrawTool.ColorPicker];
            if (EditMode == GraphicsEditMode.Arrange)
                return _arrangerTools[ArrangeTool.PickPalette];
        }

        return ResolveActiveTool();
    }

    public ToolCursor ResolveCursor(KeyModifiers modifiers)
    {
        var cursor = ResolveToolWithModifiers(modifiers)?.Cursor ?? ToolCursor.Default;
        return cursor == ToolCursor.Crosshair && HoverTarget is null ? ToolCursor.NotAllowed : cursor;
    }

    private void SetHoverTarget(Rectangle? target)
    {
        if (target == HoverTarget)
            return;

        HoverTarget = target;
        InvalidateEditor(InvalidationLevel.Overlay);
    }

    private void RefreshHoverTarget(KeyModifiers modifiers)
    {
        if (LastMousePosition is not { } pos)
        {
            SetHoverTarget(null);
            return;
        }

        var ctx = new ToolContext(pos.X, pos.Y, pos.X, pos.Y, new MouseState(false, false, false, modifiers));
        SetHoverTarget(ResolveToolWithModifiers(modifiers)?.GetTargetRect(ctx, this));
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
        SetHoverTarget(null);

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
            SetHoverTarget(null);
            return false;
        }

        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);

        LastMousePosition = new Point(xc, yc);

        // Shift+move in arranger mode for single-element selection (only when not overridden by modifier tool)
        if (EditMode == GraphicsEditMode.Arrange && _modifierOverrideTool is null && mouseState.Modifiers.HasFlag(KeyModifiers.Shift) && Paste is null)
        {
            if (TryStartNewSingleSelection(x, y))
            {
                CompleteSelection();
                SetHoverTarget(null);
                return true;
            }
        }

        var ctx = new ToolContext(x, y, xc, yc, mouseState);
        var tool = ResolveToolWithModifiers(mouseState.Modifiers);

        var result = tool?.OnMouseMove(ctx, this) ?? default;
        SetHoverTarget(tool?.GetTargetRect(ctx, this));
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
        bool handled = TryEngageModifierOverride(keyState.Key) || DispatchKey(keyState, x, y, isKeyDown: true);
        RefreshHoverTarget(keyState.Modifiers);
        return handled;
    }

    public void KeyUp(KeyState keyState, double? x, double? y)
    {
        if (!TryReleaseModifierOverride(keyState.Key))
            DispatchKey(keyState, x, y, isKeyDown: false);

        RefreshHoverTarget(keyState.Modifiers);
    }

    private bool IsModifierToolKey(Key key) => AlternativeToolKeys.Contains(key) || TertiaryToolKeys.Contains(key);

    // Modifier overrides engage and release even when the mouse is not hovering over the image
    private bool TryEngageModifierOverride(Key key)
    {
        if (_modifierOverrideTool is not null || !IsModifierToolKey(key))
            return false;

        if (EditMode == GraphicsEditMode.Draw)
            _modifierOverrideTool = _pixelTools[DrawTool.ColorPicker];
        else if (EditMode == GraphicsEditMode.Arrange)
            _modifierOverrideTool = _arrangerTools[ArrangeTool.PickPalette];
        else
            return false;

        OnPropertyChanged(nameof(DisplayedDrawTool));
        OnPropertyChanged(nameof(DisplayedArrangeTool));
        return true;
    }

    private bool TryReleaseModifierOverride(Key key)
    {
        if (_modifierOverrideTool is null || !IsModifierToolKey(key))
            return false;

        _modifierOverrideTool = null;
        OnPropertyChanged(nameof(DisplayedDrawTool));
        OnPropertyChanged(nameof(DisplayedArrangeTool));
        return true;
    }

    private bool DispatchKey(KeyState keyState, double? x, double? y, bool isKeyDown)
    {
        if (!x.HasValue || !y.HasValue)
            return false;

        int xc = Math.Clamp((int)x.Value, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y.Value, 0, WorkingArranger.ArrangerPixelSize.Height - 1);

        var ctx = new ToolContext(x.Value, y.Value, xc, yc, keyState);
        var tool = ResolveActiveTool();

        var result = (isKeyDown ? tool?.OnKeyDown(ctx, this) : tool?.OnKeyUp(ctx, this)) ?? default;
        if (result.Invalidation != InvalidationLevel.None)
            InvalidateEditor(result.Invalidation);
        return result.Handled;
    }

    internal void UpdateActivityMessage(int xc, int yc)
    {
        ActivityBrush = null;
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

    internal Rectangle GetElementRectAtPixel(int xc, int yc)
    {
        var elementSize = WorkingArranger.ElementPixelSize;
        return new Rectangle(xc / elementSize.Width * elementSize.Width, yc / elementSize.Height * elementSize.Height,
            elementSize.Width, elementSize.Height);
    }

    internal void InspectColorAtPosition(int xc, int yc)
    {
        var elX = xc / WorkingArranger.ElementPixelSize.Width;
        var elY = yc / WorkingArranger.ElementPixelSize.Height;
        var el = WorkingArranger.GetElement(elX, elY);

        if (el is { Codec: IIndexedCodec codec })
        {
            var palette = codec.Palette;
            var colorIndex = _imageAdapter.GetIndexedPixel(xc, yc);
            var nativeColor = palette.GetNativeColor(colorIndex);
            var foreignColor = palette.GetForeignColor(colorIndex);

            ActivityBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(nativeColor.R, nativeColor.G, nativeColor.B));

            var foreignHex = $"0x{foreignColor.Color:X}";
            ActivityMessage = $"Color ({xc}, {yc}): Index {colorIndex}, {Palette.ColorModelToString(palette.ColorModel)} {foreignHex}, Rgba32 ({nativeColor.R}, {nativeColor.G}, {nativeColor.B}, {nativeColor.A})";
        }
        else if (el is { Codec: IDirectCodec })
        {
            var color = _imageAdapter.GetDirectPixel(xc, yc);

            ActivityBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(color.R, color.G, color.B));
            ActivityMessage = $"Color ({xc}, {yc}): Rgba32 ({color.R}, {color.G}, {color.B}, {color.A})";
        }
        else if (el is not null)
        {
            ActivityBrush = null;
            ActivityMessage = $"Element ({elX}, {elY}): No color data";
        }
        else
        {
            ActivityBrush = null;
            ActivityMessage = $"Element ({elX}, {elY}): Empty";
        }
    }

    internal void InspectPaletteAtPosition(int xc, int yc)
    {
        ActivityBrush = null;
        var elX = xc / WorkingArranger.ElementPixelSize.Width;
        var elY = yc / WorkingArranger.ElementPixelSize.Height;
        var el = WorkingArranger.GetElement(elX, elY);

        if (el is { Codec: IIndexedCodec codec })
        {
            var palette = codec.Palette;
            var sourceName = palette.DataSource switch
            {
                FileDataSource fds => fds.FileLocation,
                MemoryDataSource => "Memory",
                _ => palette.StorageSource == PaletteStorageSource.GlobalJson ? "Global" : "None"
            };

            ActivityMessage = $"Palette: {palette.Name}, Colors {palette.Entries}, Model {Palette.ColorModelToString(palette.ColorModel)}, Source {sourceName}";
        }
        else if (el is not null)
        {
            ActivityMessage = $"Element ({elX}, {elY}): Not indexed color";
        }
        else
        {
            ActivityMessage = $"Element ({elX}, {elY}): Empty";
        }
    }

    internal void InspectElementAtPosition(int xc, int yc)
    {
        ActivityBrush = null;
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
