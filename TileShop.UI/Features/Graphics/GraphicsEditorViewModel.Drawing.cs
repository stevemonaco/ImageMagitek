using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.Features.Graphics;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

public partial class GraphicsEditorViewModel
{
    [ObservableProperty] private ColorEditorFlyoutViewModel? _colorEditorFlyout;

    public bool CanEditSelectedColor =>
        IsDrawMode && IsIndexedColor && ActivePalette is not null &&
        !ActivePalette.Palette.IsReadOnly;

    [RelayCommand]
    private void OpenColorEditorFlyout()
    {
        if (ActivePalette is null)
            return;

        ColorEditorFlyout = new ColorEditorFlyoutViewModel(
            ActivePalette.Palette,
            PrimaryColorIndex,
            _colorFactory,
            OnColorEditorConfirm);
    }

    private void OnColorEditorConfirm(Palette palette, int colorIndex, IColor newColor) =>
        Messenger.Send(new PaletteColorAssignedMessage(palette, colorIndex, _colorFactory.CloneColor(newColor)));

    [RelayCommand]
    public void ChangePixelTool(DrawTool tool)
    {
        SelectedDrawTool = tool;
    }

    [RelayCommand]
    public void CycleDrawClipEffect()
    {
        DrawClipEffect = DrawClipEffect switch
        {
            DrawClipEffect.Greyscale => DrawClipEffect.Hidden,
            DrawClipEffect.Hidden => DrawClipEffect.Greyscale,
            _ => DrawClipEffect.Greyscale,
        };
    }

    [RelayCommand]
    public void ToggleSnapMode()
    {
        if (!CanChangeSnapMode)
            return;

        if (SnapMode == SnapMode.Element)
            SnapMode = SnapMode.Pixel;
        else if (SnapMode == SnapMode.Pixel)
            SnapMode = SnapMode.Element;
    }

    private bool _pencilFailureShown;
    private (Point Pixel, Palette Palette, byte Index, bool Succeeded)? _lastPencilCheck;

    public void StartPencilDraw()
    {
        _activePencilHistory = IsIndexedColor ? new PencilHistoryAction<byte>() : new PencilHistoryAction<ColorRgba32>();
        _pencilFailureShown = false;
        _lastPencilCheck = null;
        IsPencilDrawing = true;
    }

    public void StopPencilDraw()
    {
        if (!IsPencilDrawing)
            return;

        var history = _activePencilHistory;
        IsPencilDrawing = false;
        _activePencilHistory = null;

        if (history is PencilHistoryAction<byte> { ModifiedPoints.Count: > 0 } or PencilHistoryAction<ColorRgba32> { ModifiedPoints.Count: > 0 })
            AddHistoryAction(history);
    }

    internal bool IsPointInDrawClip(int x, int y)
    {
        if (!IsDrawClipActive || DrawClipRect is not { } clip)
            return true;
        return x >= clip.SnappedLeft && x < clip.SnappedRight &&
               y >= clip.SnappedTop && y < clip.SnappedBottom;
    }

    private Rectangle? DrawClipBounds =>
        IsDrawClipActive && DrawClipRect is { } clip
            ? new Rectangle(clip.SnappedLeft, clip.SnappedTop, clip.SnappedWidth, clip.SnappedHeight)
            : null;

    internal bool CanSetPixelAtPosition(int x, int y)
    {
        if (!IsPointInDrawClip(x, y))
            return false;

        if (IsDirectColor)
            return HasColorDataAtPosition(x, y);

        if (!HasColorDataAtPosition(x, y) || !HasActivePaletteColor(PrimaryColorIndex))
            return false;

        var palette = ActivePalette!.Palette;
        if (IsPencilDrawing && _lastPencilCheck is { } check && check.Pixel == new Point(x, y)
            && ReferenceEquals(check.Palette, palette) && check.Index == PrimaryColorIndex)
            return check.Succeeded;

        return _imageAdapter.CanPaintIndex(x, y, palette, PrimaryColorIndex).HasSucceeded;
    }

    internal bool CanFloodFillAtPosition(int x, int y) => IsPointInDrawClip(x, y) && HasColorDataAtPosition(x, y);

    internal bool CanPickColorAtPosition(int x, int y) => HasColorDataAtPosition(x, y);

    private bool HasColorDataAtPosition(int x, int y) =>
        _imageAdapter.GetElementAtPixel(x, y) is { IsWithinSource: true } element && (IsDirectColor || element.Codec is IIndexedCodec);

    private bool HasActivePaletteColor(byte colorIndex) => ActivePalette is not null && colorIndex < ActivePalette.Colors.Count;

    /// <summary>
    /// Paints one stroke segment, skipping pixels outside the image or the draw clip, then repaints the touched region once.
    /// </summary>
    internal void PaintPencilPixels(IReadOnlyList<Point> points, ColorPriority priority)
    {
        var width = WorkingArranger.ArrangerPixelSize.Width;
        var height = WorkingArranger.ArrangerPixelSize.Height;
        Rectangle? dirty = null;

        foreach (var p in points)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height || !IsPointInDrawClip(p.X, p.Y))
                continue;

            var isNewPixel = IsIndexedColor
                ? PaintIndexedPixel(p.X, p.Y, priority == ColorPriority.Primary ? PrimaryColorIndex : SecondaryColorIndex)
                : PaintDirectPixel(p.X, p.Y, priority == ColorPriority.Primary ? PrimaryColor : SecondaryColor);

            if (isNewPixel)
                dirty = dirty is { } rect ? Rectangle.Union(rect, new Rectangle(p.X, p.Y, 1, 1)) : new Rectangle(p.X, p.Y, 1, 1);
        }

        if (dirty is not { } dirtyRect)
            return;

        IsModified = true;
        EditorBitmap.Invalidate(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);
        OnImageModified?.Invoke();
    }

    private bool PaintIndexedPixel(int x, int y, byte colorIndex)
    {
        if (!HasActivePaletteColor(colorIndex))
            return false;

        var palette = ActivePalette!.Palette;
        var result = _imageAdapter.TryPaintIndex(x, y, palette, colorIndex);
        _lastPencilCheck = (new Point(x, y), palette, colorIndex, result.HasSucceeded);

        return result.Match(
            success => _activePencilHistory is PencilHistoryAction<byte> pencilHistory && pencilHistory.Add(x, y, success.Result),
            fail =>
            {
                if (!_pencilFailureShown)
                {
                    _pencilFailureShown = true;
                    Messenger.Send(new NotifyStatusMessage(fail.Reason));
                }
                return false;
            });
    }

    private bool PaintDirectPixel(int x, int y, ColorRgba32 color)
    {
        _imageAdapter.SetDirectPixel(x, y, color);
        return _activePencilHistory is PencilHistoryAction<ColorRgba32> pencilHistory && pencilHistory.Add(x, y, color);
    }

    internal void FloodFillAtPosition(int x, int y, ColorPriority priority)
    {
        if (!IsPointInDrawClip(x, y))
            return;

        var clipBounds = DrawClipBounds;

        if (IsIndexedColor)
        {
            var colorIndex = priority == ColorPriority.Primary ? PrimaryColorIndex : SecondaryColorIndex;
            if (_imageAdapter.FloodFill(x, y, colorIndex, clipBounds))
            {
                AddHistoryAction(new FloodFillAction<byte>(x, y, colorIndex, clipBounds));
                IsModified = true;
            }
        }
        else
        {
            var color = priority == ColorPriority.Primary ? PrimaryColor : SecondaryColor;
            if (_imageAdapter.FloodFill(x, y, color, clipBounds))
            {
                AddHistoryAction(new FloodFillAction<ColorRgba32>(x, y, color, clipBounds));
                IsModified = true;
            }
        }
    }

    public bool PickColor(int x, int y, ColorPriority priority)
    {
        if (IsIndexedColor)
        {
            var el = _imageAdapter.GetElementAtPixel(x, y);

            if (el is ArrangerElement { Codec: IIndexedCodec codec } element)
            {
                ActivePalette = FindOrAddPalette(codec.Palette);
                var colorIndex = _imageAdapter.GetIndexedPixel(x, y);

                if (priority == ColorPriority.Primary)
                    PrimaryColorIndex = colorIndex;
                else
                    SecondaryColorIndex = colorIndex;

                return true;
            }
        }
        else
        {
            var color = _imageAdapter.GetDirectPixel(x, y);

            if (priority == ColorPriority.Primary)
                PrimaryColor = color;
            else
                SecondaryColor = color;

            return true;
        }

        return false;
    }
}
