using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagitek.Colors;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

public partial class GraphicsEditorViewModel
{
    [ObservableProperty] private ObservableCollection<HistoryAction> _undoHistory = new();
    [ObservableProperty] private ObservableCollection<HistoryAction> _redoHistory = new();

    public bool CanUndo => UndoHistory.Count > 0;
    public bool CanRedo => RedoHistory.Count > 0;
    
    public override void ApplyHistoryAction(HistoryAction action)
    {
        if (action is PencilHistoryAction<byte> indexedPencilAction && IsIndexedColor)
        {
            foreach (var point in indexedPencilAction.ModifiedPoints)
                _imageAdapter.SetIndexedPixel(point.X, point.Y, indexedPencilAction.PencilColor);
        }
        else if (action is PencilHistoryAction<ColorRgba32> directPencilAction && IsDirectColor)
        {
            foreach (var point in directPencilAction.ModifiedPoints)
                _imageAdapter.SetDirectPixel(point.X, point.Y, directPencilAction.PencilColor);
        }
        else if (action is FloodFillAction<byte> indexedFloodFillAction && IsIndexedColor)
        {
            _imageAdapter.FloodFill(indexedFloodFillAction.X, indexedFloodFillAction.Y, indexedFloodFillAction.FillColor);
        }
        else if (action is FloodFillAction<ColorRgba32> directFloodFillAction && IsDirectColor)
        {
            _imageAdapter.FloodFill(directFloodFillAction.X, directFloodFillAction.Y, directFloodFillAction.FillColor);
        }
        else if (action is ColorRemapHistoryAction remapAction && IsIndexedColor)
        {
            _imageAdapter.RemapColors(remapAction.FinalColors.Select(x => (byte)x.Index).ToList());
        }
        else if (action is PasteArrangerHistoryAction pasteAction)
        {
            ApplyPasteInternal(pasteAction.Paste);
        }
    }

    public override void AddHistoryAction(HistoryAction action)
    {
        UndoHistory.Add(action);
        RedoHistory.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void ClearHistory()
    {
        UndoHistory.Clear();
        RedoHistory.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    [RelayCommand]
    public override void Undo()
    {
        if (!CanUndo)
            return;

        var lastAction = UndoHistory[^1];
        UndoHistory.RemoveAt(UndoHistory.Count - 1);
        RedoHistory.Add(lastAction);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));

        IsModified = UndoHistory.Count > 0;

        ReloadImage();

        foreach (var action in UndoHistory)
            ApplyHistoryAction(action);

        InvalidateEditor(InvalidationLevel.PixelData);
    }

    [RelayCommand]
    public override void Redo()
    {
        if (!CanRedo)
            return;

        var redoAction = RedoHistory[^1];
        RedoHistory.RemoveAt(RedoHistory.Count - 1);
        UndoHistory.Add(redoAction);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));

        ApplyHistoryAction(redoAction);
        IsModified = true;
        InvalidateEditor(InvalidationLevel.PixelData);
    }
}
