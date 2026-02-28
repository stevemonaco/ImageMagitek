using System;
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
        ActivePalette.Palette.StorageSource != PaletteStorageSource.GlobalJson;

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

    private void OnColorEditorConfirm(Palette palette, int colorIndex, IColor newColor)
    {
        palette.SetForeignColor(colorIndex, newColor);
        palette.SavePalette();

        // Update the swatch grid
        var nativeColor = _colorFactory.ToNative(newColor);
        var mediaColor = global::Avalonia.Media.Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);

        if (ActivePalette is not null && colorIndex < ActivePalette.Colors.Count)
            ActivePalette.Colors[colorIndex] = new PaletteEntry((byte)colorIndex, mediaColor);

        // Re-render and broadcast change
        InvalidateEditor(InvalidationLevel.PixelData);
        Messenger.Send(new PaletteChangedMessage(palette));
    }

    [RelayCommand]
    public void ChangePixelTool(PixelTool tool)
    {
        ActivePixelTool = tool;
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
        if (SnapMode == SnapMode.Element)
            SnapMode = SnapMode.Pixel;
        else if (SnapMode == SnapMode.Pixel)
            SnapMode = SnapMode.Element;
    }

    // [RelayCommand]
    // public void SwitchToArrangerMode()
    // {
    //     EditMode = GraphicsEditMode.Arrange;
    // }
    //
    // [RelayCommand]
    // public void SwitchToPixelMode()
    // {
    //     EditMode = GraphicsEditMode.Draw;
    // }

    public void StartPencilDraw(int x, int y, ColorPriority priority)
    {
        if (IsIndexedColor)
        {
            var colorIndex = priority == ColorPriority.Primary ? PrimaryColorIndex : SecondaryColorIndex;
            _activePencilHistory = new PencilHistoryAction<byte>(colorIndex);
        }
        else
        {
            var color = priority == ColorPriority.Primary ? PrimaryColor : SecondaryColor;
            _activePencilHistory = new PencilHistoryAction<ColorRgba32>(color);
        }
        IsPencilDrawing = true;
    }

    public void StopPencilDraw()
    {
        if (!IsPencilDrawing)
            return;

        if (IsIndexedColor && _activePencilHistory is PencilHistoryAction<byte> indexedHistory && indexedHistory.ModifiedPoints.Count > 0)
        {
            IsPencilDrawing = false;
            AddHistoryAction(indexedHistory);
            _activePencilHistory = null;
        }
        else if (IsDirectColor && _activePencilHistory is PencilHistoryAction<ColorRgba32> directHistory && directHistory.ModifiedPoints.Count > 0)
        {
            IsPencilDrawing = false;
            AddHistoryAction(directHistory);
            _activePencilHistory = null;
        }
    }

    internal bool IsPointInDrawClip(int x, int y)
    {
        if (!IsDrawClipActive || DrawClipRect is not { } clip)
            return true;
        return x >= clip.SnappedLeft && x < clip.SnappedRight &&
               y >= clip.SnappedTop && y < clip.SnappedBottom;
    }

    internal void SetPixelAtPosition(int x, int y, ColorPriority priority)
    {
        if (!IsPointInDrawClip(x, y))
            return;

        if (IsIndexedColor)
        {
            var colorIndex = priority == ColorPriority.Primary ? PrimaryColorIndex : SecondaryColorIndex;
            SetIndexedPixel(x, y, colorIndex);
        }
        else
        {
            var color = priority == ColorPriority.Primary ? PrimaryColor : SecondaryColor;
            SetDirectPixel(x, y, color);
        }
    }

    private void SetIndexedPixel(int x, int y, byte colorIndex)
    {
        if (ActivePalette is null)
            return;

        var modelColor = ActivePalette.Colors[colorIndex].Color;
        var palColor = new ColorRgba32(modelColor.R, modelColor.G, modelColor.B, modelColor.A);
        var result = _imageAdapter.TrySetPixel(x, y, palColor);

        var message = result.Match(
            _ =>
            {
                if (_activePencilHistory is PencilHistoryAction<byte> pencilHistory && pencilHistory.ModifiedPoints.Add(new Point(x, y)))
                {
                    IsModified = true;
                    BitmapAdapter.Invalidate(x, y, 1, 1);
                    OnImageModified?.Invoke();
                }
                return new NotifyStatusMessage("");
            },
            fail => new NotifyStatusMessage(fail.Reason)
        );
        Messenger.Send(message);
    }

    private void SetDirectPixel(int x, int y, ColorRgba32 color)
    {
        _imageAdapter.SetDirectPixel(x, y, color);

        if (_activePencilHistory is PencilHistoryAction<ColorRgba32> pencilHistory && pencilHistory.ModifiedPoints.Add(new Point(x, y)))
        {
            IsModified = true;
            BitmapAdapter.Invalidate(x, y, 1, 1);
            OnImageModified?.Invoke();
        }
    }

    internal void FloodFillAtPosition(int x, int y, ColorPriority priority)
    {
        if (!IsPointInDrawClip(x, y))
            return;

        Rectangle? clipBounds = null;
        if (IsDrawClipActive && DrawClipRect is { } clip)
            clipBounds = new Rectangle(clip.SnappedLeft, clip.SnappedTop, clip.SnappedWidth, clip.SnappedHeight);

        if (IsIndexedColor)
        {
            var colorIndex = priority == ColorPriority.Primary ? PrimaryColorIndex : SecondaryColorIndex;
            if (_imageAdapter.FloodFill(x, y, colorIndex, clipBounds))
            {
                AddHistoryAction(new FloodFillAction<byte>(x, y, colorIndex));
                IsModified = true;
            }
        }
        else
        {
            var color = priority == ColorPriority.Primary ? PrimaryColor : SecondaryColor;
            if (_imageAdapter.FloodFill(x, y, color, clipBounds))
            {
                AddHistoryAction(new FloodFillAction<ColorRgba32>(x, y, color));
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
                ActivePalette = Palettes.FirstOrDefault(p => ReferenceEquals(p.Palette, codec.Palette));
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

    [RelayCommand]
    public void SetPrimaryColorIndex(byte colorIndex) => PrimaryColorIndex = colorIndex;

    [RelayCommand]
    public void SetSecondaryColorIndex(byte colorIndex) => SecondaryColorIndex = colorIndex;

    [RelayCommand]
    public void SetPrimaryColor(ColorRgba32 color) => PrimaryColor = color;

    [RelayCommand]
    public void SetSecondaryColor(ColorRgba32 color) => SecondaryColor = color;
}
