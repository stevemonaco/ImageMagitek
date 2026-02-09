using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;

namespace TileShop.UI.Features.Graphics;

public partial class GraphicsEditorViewModel
{
    [RelayCommand]
    public void SelectAll()
    {
        CancelOverlay();
        Selection = new ArrangerSelection(WorkingArranger, SnapMode);
        Selection.StartSelection(0, 0);
        Selection.UpdateSelectionEndpoint(WorkingArranger.ArrangerPixelSize.Width, WorkingArranger.ArrangerPixelSize.Height);
        CompleteSelection();
        OnPropertyChanged(nameof(CanEditSelection));
    }

    [RelayCommand]
    public void CancelOverlay()
    {
        Selection = new ArrangerSelection(WorkingArranger, SnapMode);
        Paste = null;
        ActivityMessage = string.Empty;
        PendingOperationMessage = string.Empty;

        OnPropertyChanged(nameof(CanEditSelection));
    }

    [RelayCommand]
    public void EditSelection()
    {
        if (!CanEditSelection)
            return;

        EditArrangerPixelsMessage editMessage;
        var rect = Selection.SelectionRect;

        if (SnapMode == SnapMode.Element && WorkingArranger.Layout == ElementLayout.Tiled)
        {
            WorkingArranger.CopyElements();
            var arranger = WorkingArranger.CloneArranger(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
            editMessage = new EditArrangerPixelsMessage(arranger, (Arranger)Resource, 0, 0, rect.SnappedWidth, rect.SnappedHeight);
        }
        else
        {
            var arranger = WorkingArranger.CloneArranger();
            editMessage = new EditArrangerPixelsMessage(arranger, (Arranger)Resource, rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
        }

        WeakReferenceMessenger.Default.Send(editMessage);
        CancelOverlay();
    }

    [RelayCommand]
    public void ConfirmPendingOperation()
    {
        if (Paste?.Copy is ElementCopy or IndexedPixelCopy or DirectPixelCopy)
            ApplyPaste(Paste);
    }

    public void StartNewSelection(double x, double y)
    {
        Selection.StartSelection(x, y);
        IsSelecting = true;
    }

    public bool TryStartNewSingleSelection(double x, double y)
    {
        var rect = Selection.SelectionRect;
        if (Selection.HasSelection && rect.SnapMode == SnapMode.Element &&
            rect.SnappedWidth == WorkingArranger.ElementPixelSize.Width &&
            rect.SnappedHeight == WorkingArranger.ElementPixelSize.Height)
        {
            if (rect.ContainsPointSnapped(x, y))
                return false;
        }

        CancelOverlay();
        Selection.StartSelection(x, y);
        IsSelecting = true;
        return true;
    }

    public void UpdateSelection(double x, double y)
    {
        if (IsSelecting)
            Selection.UpdateSelectionEndpoint(x, y);
    }

    public void CompleteSelection()
    {
        if (IsSelecting)
        {
            if (Selection.SelectionRect.SnappedWidth == 0 || Selection.SelectionRect.SnappedHeight == 0)
            {
                Selection = new ArrangerSelection(WorkingArranger, SnapMode);
            }

            IsSelecting = false;
            OnPropertyChanged(nameof(CanEditSelection));
        }
    }

    public void CompletePaste()
    {
        if (Paste is not null)
        {
            ApplyPaste(Paste);
        }
    }
}
