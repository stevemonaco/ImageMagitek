using System;
using System.Collections.ObjectModel;
using System.Drawing;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Image;
using TileShop.Shared.Models;
using TileShop.UI.Models;

namespace TileShop.UI.Features.Graphics;

/// <summary>
/// Undo and redo for the graphics editor. Arranger actions restore snapshots; pixel actions re-render from source and replay.
/// </summary>
public sealed class GraphicsEditHistory
{
    private readonly ObservableCollection<HistoryAction> _undo;
    private readonly ObservableCollection<HistoryAction> _redo;
    private readonly ICodecFactory _codecFactory;
    private ArrangerSnapshot? _base;

    public GraphicsEditHistory(ObservableCollection<HistoryAction> undo, ObservableCollection<HistoryAction> redo, Arranger working,
        ICodecFactory codecFactory)
    {
        _undo = undo;
        _redo = redo;
        _codecFactory = codecFactory;
        _base = CaptureBase(working);
    }

    /// <summary>
    /// Clears both histories and makes <paramref name="working"/> the state that undoing everything returns to
    /// </summary>
    public void Reset(Arranger working)
    {
        _undo.Clear();
        _redo.Clear();
        _base = CaptureBase(working);
    }

    public Arranger RestoreBase() => _base!.Restore(_codecFactory);

    public void Add(HistoryAction action, Arranger working)
    {
        if (action is ArrangerHistoryAction arrangerAction)
            arrangerAction.After = ArrangerSnapshot.Capture(working);

        _undo.Add(action);
        _redo.Clear();
    }

    /// <returns>True when the image was given a new arranger</returns>
    public bool Undo(ArrangerImageAdapter image)
    {
        var undone = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(undone);

        int restoreIndex = _undo.Count - 1;
        while (restoreIndex >= 0 && _undo[restoreIndex] is not ArrangerHistoryAction)
            restoreIndex--;

        var restorePoint = restoreIndex >= 0 ? ((ArrangerHistoryAction)_undo[restoreIndex]).After : _base;
        bool replaced = false;

        if (undone is ArrangerHistoryAction && restorePoint is not null)
        {
            image.Reinitialize(restorePoint.Restore(_codecFactory));
            replaced = true;
        }
        else
        {
            image.Render();
        }

        ReplayPixelActions(image);
        return replaced;
    }

    /// <returns>True when the image was given a new arranger</returns>
    public bool Redo(ArrangerImageAdapter image)
    {
        var action = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(action);

        if (action is ArrangerHistoryAction { After: { } after })
        {
            image.Reinitialize(after.Restore(_codecFactory));
            ReplayPixelActions(image);
            return true;
        }

        Apply(action, image);
        return false;
    }

    public static void Apply(HistoryAction action, ArrangerImageAdapter image)
    {
        switch (action)
        {
            case PencilHistoryAction<byte> pencil when image.IsIndexed:
                foreach (var point in pencil.ModifiedPoints)
                    image.SetIndexedPixel(point.X, point.Y, pencil.PencilColor);
                break;
            case PencilHistoryAction<ColorRgba32> pencil when image.IsDirect:
                foreach (var point in pencil.ModifiedPoints)
                    image.SetDirectPixel(point.X, point.Y, pencil.PencilColor);
                break;
            case FloodFillAction<byte> fill when image.IsIndexed:
                image.FloodFill(fill.X, fill.Y, fill.FillColor, fill.ClipBounds);
                break;
            case FloodFillAction<ColorRgba32> fill when image.IsDirect:
                image.FloodFill(fill.X, fill.Y, fill.FillColor, fill.ClipBounds);
                break;
            case ColorRemapHistoryAction remap when image.IsIndexed:
                image.RemapColors(remap.Remap, remap.Bounds);
                break;
            case PasteArrangerHistoryAction paste:
                ApplyPixelPaste(image, paste.Copy, paste.X, paste.Y, paste.ClipBounds);
                break;
        }
    }

    /// <summary>
    /// Copies the pixels of <paramref name="copy"/> into the image at (<paramref name="pasteX"/>, <paramref name="pasteY"/>), limited to <paramref name="clip"/>
    /// </summary>
    public static MagitekResult ApplyPixelPaste(ArrangerImageAdapter image, ArrangerCopy copy, int pasteX, int pasteY, Rectangle? clip)
    {
        int clipLeft = 0;
        int clipTop = 0;
        int clipRight = image.Width;
        int clipBottom = image.Height;

        if (clip is { } bounds)
        {
            clipLeft = Math.Max(clipLeft, bounds.Left);
            clipTop = Math.Max(clipTop, bounds.Top);
            clipRight = Math.Min(clipRight, bounds.Right);
            clipBottom = Math.Min(clipBottom, bounds.Bottom);
        }

        int destX = Math.Max(clipLeft, pasteX);
        int destY = Math.Max(clipTop, pasteY);
        int sourceX = pasteX >= clipLeft ? 0 : clipLeft - pasteX;
        int sourceY = pasteY >= clipTop ? 0 : clipTop - pasteY;

        var destStart = new Point(destX, destY);
        var sourceStart = new Point(sourceX, sourceY);

        var pixelCopy = copy is ElementCopy elementCopy ? elementCopy.ToPixelCopy() : copy;

        if (image.IsIndexed)
        {
            var destImage = image.IndexedImage!;

            if (pixelCopy is IndexedPixelCopy indexedCopy)
            {
                int copyWidth = Math.Min(indexedCopy.Width - sourceX, clipRight - destX);
                int copyHeight = Math.Min(indexedCopy.Height - sourceY, clipBottom - destY);

                if (copyWidth <= 0 || copyHeight <= 0)
                    return MagitekResult.SuccessResult;

                return ImageCopier.CopyPixels(indexedCopy.Image, destImage, sourceStart, destStart,
                    copyWidth, copyHeight,
                    PixelRemapOperation.RemapByExactIndex,
                    PixelRemapOperation.RemapByExactPaletteColors);
            }
            else if (pixelCopy is DirectPixelCopy directCopy)
            {
                int copyWidth = Math.Min(directCopy.Width - sourceX, clipRight - destX);
                int copyHeight = Math.Min(directCopy.Height - sourceY, clipBottom - destY);

                if (copyWidth <= 0 || copyHeight <= 0)
                    return MagitekResult.SuccessResult;

                return ImageCopier.CopyPixels(directCopy.Image, destImage, sourceStart, destStart,
                    copyWidth, copyHeight,
                    PixelRemapOperation.RemapByExactPaletteColors);
            }
        }
        else
        {
            var destImage = image.DirectImage!;

            if (pixelCopy is DirectPixelCopy directCopy)
            {
                int copyWidth = Math.Min(directCopy.Width - sourceX, clipRight - destX);
                int copyHeight = Math.Min(directCopy.Height - sourceY, clipBottom - destY);

                if (copyWidth <= 0 || copyHeight <= 0)
                    return MagitekResult.SuccessResult;

                return ImageCopier.CopyPixels(directCopy.Image, destImage, sourceStart, destStart,
                    copyWidth, copyHeight);
            }
            else if (pixelCopy is IndexedPixelCopy indexedCopy)
            {
                int copyWidth = Math.Min(indexedCopy.Width - sourceX, clipRight - destX);
                int copyHeight = Math.Min(indexedCopy.Height - sourceY, clipBottom - destY);

                if (copyWidth <= 0 || copyHeight <= 0)
                    return MagitekResult.SuccessResult;

                return ImageCopier.CopyPixels(indexedCopy.Image, destImage, sourceStart, destStart,
                    copyWidth, copyHeight);
            }
        }

        return new MagitekResult.Failed($"Unknown copy type: {copy.GetType()}");
    }

    /// <summary>
    /// Replays the pixel actions after the last arranger action that re-rendered from source. Apply Palette keeps the pixels it was applied over.
    /// </summary>
    private void ReplayPixelActions(ArrangerImageAdapter image)
    {
        int start = _undo.Count - 1;
        while (start >= 0 && _undo[start] is (not ArrangerHistoryAction) or ApplyPaletteHistoryAction)
            start--;

        for (int i = start + 1; i < _undo.Count; i++)
            Apply(_undo[i], image);
    }

    private static ArrangerSnapshot? CaptureBase(Arranger working) =>
        working is ScatteredArranger ? ArrangerSnapshot.Capture(working) : null;
}
