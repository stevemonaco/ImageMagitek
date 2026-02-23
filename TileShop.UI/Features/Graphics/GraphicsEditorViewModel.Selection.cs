using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;

public partial class GraphicsEditorViewModel
{
    private const double HandleScreenSize = 8.0;

    [ObservableProperty] private SelectionHandle _activeResizeHandle = SelectionHandle.None;
    [ObservableProperty] private bool _isResizing;

    [RelayCommand]
    public void SelectAll()
    {
        CancelOverlay();
        Selection = new ArrangerSelection(WorkingArranger, SnapMode);
        Selection.StartSelection(0, 0);
        Selection.UpdateSelectionEndpoint(WorkingArranger.ArrangerPixelSize.Width, WorkingArranger.ArrangerPixelSize.Height);
        CompleteSelection();
        OnPropertyChanged(nameof(CanEditSelection));
        OnImageModified?.Invoke();
    }

    [RelayCommand]
    public void CancelOverlay()
    {
        Selection = new ArrangerSelection(WorkingArranger, SnapMode);
        Paste = null;
        ActivityMessage = string.Empty;
        PendingOperationMessage = string.Empty;

        OnPropertyChanged(nameof(CanEditSelection));
        OnImageModified?.Invoke();
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
        {
            Selection.UpdateSelectionEndpoint(x, y);
            OnImageModified?.Invoke();
        }
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
            OnImageModified?.Invoke();
        }
    }

    public void CompletePaste()
    {
        if (Paste is not null)
        {
            ApplyPaste(Paste);
        }
    }

    public SelectionHandle HitTestHandle(double x, double y)
    {
        if (!Selection.HasSelection)
            return SelectionHandle.None;

        var sel = Selection.SelectionRect;
        var handleSize = HandleScreenSize / Math.Max(Zoom, 0.01);
        var halfHandle = handleSize / 2.0;

        double left = sel.SnappedLeft;
        double right = sel.SnappedRight;
        double top = sel.SnappedTop;
        double bottom = sel.SnappedBottom;
        double midX = (left + right) / 2.0;
        double midY = (top + bottom) / 2.0;

        if (IsInHandle(x, y, left, top, halfHandle)) return SelectionHandle.TopLeft;
        if (IsInHandle(x, y, midX, top, halfHandle)) return SelectionHandle.Top;
        if (IsInHandle(x, y, right, top, halfHandle)) return SelectionHandle.TopRight;
        if (IsInHandle(x, y, right, midY, halfHandle)) return SelectionHandle.Right;
        if (IsInHandle(x, y, right, bottom, halfHandle)) return SelectionHandle.BottomRight;
        if (IsInHandle(x, y, midX, bottom, halfHandle)) return SelectionHandle.Bottom;
        if (IsInHandle(x, y, left, bottom, halfHandle)) return SelectionHandle.BottomLeft;
        if (IsInHandle(x, y, left, midY, halfHandle)) return SelectionHandle.Left;

        return SelectionHandle.None;
    }

    private static bool IsInHandle(double x, double y, double handleX, double handleY, double halfHandle)
    {
        return x >= handleX - halfHandle && x <= handleX + halfHandle &&
               y >= handleY - halfHandle && y <= handleY + halfHandle;
    }

    public void StartResize(SelectionHandle handle)
    {
        IsResizing = true;
        ActiveResizeHandle = handle;
    }

    public void UpdateResize(double x, double y)
    {
        if (!IsResizing || ActiveResizeHandle == SelectionHandle.None)
            return;

        var rect = Selection.SelectionRect;

        switch (ActiveResizeHandle)
        {
            case SelectionHandle.TopLeft:
                rect.SetLeftEdge(x);
                rect.SetTopEdge(y);
                break;
            case SelectionHandle.Top:
                rect.SetTopEdge(y);
                break;
            case SelectionHandle.TopRight:
                rect.SetRightEdge(x);
                rect.SetTopEdge(y);
                break;
            case SelectionHandle.Right:
                rect.SetRightEdge(x);
                break;
            case SelectionHandle.BottomRight:
                rect.SetRightEdge(x);
                rect.SetBottomEdge(y);
                break;
            case SelectionHandle.Bottom:
                rect.SetBottomEdge(y);
                break;
            case SelectionHandle.BottomLeft:
                rect.SetLeftEdge(x);
                rect.SetBottomEdge(y);
                break;
            case SelectionHandle.Left:
                rect.SetLeftEdge(x);
                break;
        }

        OnImageModified?.Invoke();
    }

    public void CompleteResize()
    {
        if (IsResizing)
        {
            var rect = Selection.SelectionRect;
            if (rect.SnappedWidth == 0 || rect.SnappedHeight == 0)
            {
                Selection = new ArrangerSelection(WorkingArranger, SnapMode);
            }

            IsResizing = false;
            ActiveResizeHandle = SelectionHandle.None;
            OnPropertyChanged(nameof(CanEditSelection));
            OnImageModified?.Invoke();
        }
    }
}
