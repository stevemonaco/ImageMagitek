﻿using System;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using TileShop.AvaloniaUI.Imaging;
using TileShop.AvaloniaUI.Models;
using TileShop.Shared.Models;
using TileShop.Shared.EventModels;
using ImageMagitek.Image;
using System.Drawing;
using ImageMagitek.ExtensionMethods;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;
using TileShop.Shared.Interactions;

namespace TileShop.AvaloniaUI.ViewModels;

public sealed partial class DirectPixelEditorViewModel : PixelEditorViewModel<ColorRgba32>
{
    private DirectImage _directImage = null!;

    public DirectPixelEditorViewModel(Arranger arranger, Arranger projectArranger,
        IInteractionService interactionService, IPaletteService paletteService)
        : base(projectArranger, interactionService, paletteService)
    {
        Initialize(arranger, 0, 0, arranger.ArrangerPixelSize.Width, arranger.ArrangerPixelSize.Height);
    }

    public DirectPixelEditorViewModel(Arranger arranger, Arranger projectArranger, int viewX, int viewY, int viewWidth, int viewHeight,
        IInteractionService interactionService, IPaletteService paletteService)
        : base(projectArranger, interactionService, paletteService)
    {
        Initialize(arranger, viewX, viewY, viewWidth, viewHeight);
    }

    private void Initialize(Arranger arranger, int viewDx, int viewDy, int viewWidth, int viewHeight)
    {
        Resource = arranger;
        WorkingArranger = arranger.CloneArranger();
        ViewDx = viewDx;
        ViewDy = viewDy;
        _viewWidth = viewWidth;
        _viewHeight = viewHeight;

        _directImage = new DirectImage(WorkingArranger, ViewDx, ViewDy, _viewWidth, _viewHeight);
        BitmapAdapter = new DirectBitmapAdapter(_directImage);

        DisplayName = $"Pixel Editor - {WorkingArranger.Name}";
        Selection = new ArrangerSelection(arranger, SnapMode);
        CreateGridlines();

        PrimaryColor = new ColorRgba32(255, 255, 255, 255);
        SecondaryColor = new ColorRgba32(0, 0, 0, 255);
    }

    public override void Render()
    {
        BitmapAdapter.Invalidate();
        OnImageModified?.Invoke();
    }

    protected override void ReloadImage() => _directImage.Render();

    [RelayCommand]
    public override async Task SaveChangesAsync()
    {
        try
        {
            _directImage.SaveImage();

            UndoHistory.Clear();
            RedoHistory.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));

            IsModified = false;
            var changeEvent = new ArrangerChangedEvent(_projectArranger, ArrangerChange.Pixels);
            Messenger.Send(changeEvent);
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync($"Could not save the pixel arranger contents\n{ex.Message}\n{ex.StackTrace}", "Save Error");
        }
    }

    public override void DiscardChanges()
    {
        _directImage.Render();
        UndoHistory.Clear();
        RedoHistory.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public override ColorRgba32 GetPixel(int x, int y) => _directImage.GetPixel(x, y);

    public override void SetPixel(int x, int y, ColorRgba32 color)
    {
        _directImage.SetPixel(x, y, color);

        if (_activePencilHistory is not null && _activePencilHistory.ModifiedPoints.Add(new Point(x, y)))
        {
            IsModified = true;
            BitmapAdapter.Invalidate(x, y, 1, 1);
        }
    }

    public override void ApplyHistoryAction(HistoryAction action)
    {
        if (action is PencilHistoryAction<ColorRgba32> pencilAction)
        {
            foreach (var point in pencilAction.ModifiedPoints)
            {
                _directImage.SetPixel(point.X, point.Y, pencilAction.PencilColor);
            }
        }
    }

    [RelayCommand]
    public override void ApplyPaste(ArrangerPaste paste)
    {
        var notifyEvent = ApplyPasteInternal(paste).Match(
            success =>
            {
                AddHistoryAction(new PasteArrangerHistoryAction(paste));

                IsModified = true;
                CancelOverlay();
                BitmapAdapter.Invalidate();

                return new NotifyStatusEvent("Paste successfully applied");
            },
            fail => new NotifyStatusEvent(fail.Reason)
            );

        Messenger.Send(notifyEvent);
    }

    private MagitekResult ApplyPasteInternal(ArrangerPaste paste)
    {
        int destX = Math.Max(0, paste.Rect.SnappedLeft);
        int destY = Math.Max(0, paste.Rect.SnappedTop);
        int sourceX = paste.Rect.SnappedLeft >= 0 ? 0 : -paste.Rect.SnappedLeft;
        int sourceY = paste.Rect.SnappedTop >= 0 ? 0 : -paste.Rect.SnappedTop;

        var destStart = new Point(destX, destY);
        var sourceStart = new Point(sourceX, sourceY);

        ArrangerCopy? copy = default;

        if (paste.Copy is ElementCopy elementCopy)
            copy = elementCopy.ToPixelCopy();
        else
            copy = paste.Copy;

        if (copy is IndexedPixelCopy indexedCopy)
        {
            int copyWidth = Math.Min(copy.Width - sourceX, _directImage.Width - destX);
            int copyHeight = Math.Min(copy.Height - sourceY, _directImage.Height - destY);

            return ImageCopier.CopyPixels(indexedCopy.Image, _directImage, sourceStart, destStart, copyWidth, copyHeight);
        }
        else if (copy is DirectPixelCopy directCopy)
        {
            int copyWidth = Math.Min(copy.Width - sourceX, _directImage.Width - destX);
            int copyHeight = Math.Min(copy.Height - sourceY, _directImage.Height - destY);

            return ImageCopier.CopyPixels(directCopy.Image, _directImage, sourceStart, destStart, copyWidth, copyHeight);
        }
        else
            throw new InvalidOperationException($"{nameof(ApplyPasteInternal)} attempted to copy from an arranger of type '{paste.Copy.Source.ColorType}' to {WorkingArranger.ColorType}");
    }

    public override void FloodFill(int x, int y, ColorRgba32 fillColor)
    {
        if (_directImage.FloodFill(x, y, fillColor))
        {
            AddHistoryAction(new FloodFillAction<ColorRgba32>(x, y, fillColor));
            IsModified = true;
            Render();
        }
    }
}
