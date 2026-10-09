using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using TileShop.Shared.Messages;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.Models;

namespace TileShop.UI.ViewModels;

public partial class GraphicsEditorViewModel
{
    private const double _handleScreenSize = 8.0;

    private static ArrangerCopy? _clipboard;

    [ObservableProperty] private SelectionHandle _activeResizeHandle = SelectionHandle.None;
    [ObservableProperty] private bool _isResizing;

    [RelayCommand]
    public void SelectAll()
    {
        CancelOverlay();

        double left = 0, top = 0;
        double right = WorkingArranger.ArrangerPixelSize.Width;
        double bottom = WorkingArranger.ArrangerPixelSize.Height;

        if (IsDrawMode && IsDrawClipActive && DrawClipRect is { } clip)
        {
            left = clip.SnappedLeft;
            top = clip.SnappedTop;
            right = clip.SnappedRight;
            bottom = clip.SnappedBottom;
        }

        Selection = new ArrangerSelection(WorkingArranger, SnapMode);
        Selection.StartSelection(left, top);
        Selection.UpdateSelectionEndpoint(right, bottom);
        CompleteSelection();
        OnPropertyChanged(nameof(CanSetDrawClipFromSelection));
        OnPropertyChanged(nameof(CanAddSelectionAsScatteredArranger));
        NotifySelectionStateChanged();
        InvalidateEditor(InvalidationLevel.Overlay);
    }

    private bool HasSelection => Selection.HasSelection;
    private static bool HasClipboard => _clipboard is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    public void CopySelection()
    {
        if (!Selection.HasSelection)
            return;

        var rect = Selection.SelectionRect;
        var arranger = WorkingArranger;

        if (SnapMode == SnapMode.Element)
        {
            int x = rect.SnappedLeft / arranger.ElementPixelSize.Width;
            int y = rect.SnappedTop / arranger.ElementPixelSize.Height;
            int width = rect.SnappedWidth / arranger.ElementPixelSize.Width;
            int height = rect.SnappedHeight / arranger.ElementPixelSize.Height;
            var copy = arranger.CopyElements(x, y, width, height);
            copy.ProjectResource = OriginatingProjectResource;
            _clipboard = copy;
        }
        else if (SnapMode == SnapMode.Pixel && arranger.ColorType == PixelColorType.Indexed)
        {
            _clipboard = arranger.CopyPixelsIndexed(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
        }
        else if (SnapMode == SnapMode.Pixel && arranger.ColorType == PixelColorType.Direct)
        {
            _clipboard = arranger.CopyPixelsDirect(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
        }

        PasteFromClipboardCommand.NotifyCanExecuteChanged();
    }

    private bool CanImportIntoSelection => HasSelection && Resource is ScatteredArranger && !WorkingArranger.IsReadOnly();

    [RelayCommand(CanExecute = nameof(CanImportIntoSelection))]
    public void ImportIntoSelection()
    {
        if (!CanImportIntoSelection)
            return;

        var rect = Selection.SelectionRect;
        var bounds = new System.Drawing.Rectangle(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
        Messenger.Send(new ImportImageIntoArrangerMessage((ScatteredArranger)Resource, bounds));
    }

    [RelayCommand(CanExecute = nameof(CanDeleteElementSelection))]
    public void CutSelection()
    {
        CopySelection();
        DeleteElementSelection();
    }

    [RelayCommand(CanExecute = nameof(HasClipboard))]
    public async Task PasteFromClipboardAsync()
    {
        if (_clipboard is null)
            return;

        bool isElementPasteInArrangeMode = _clipboard is ElementCopy
            && EditMode == GraphicsEditMode.Arrange
            && IsTiledLayout
            && WorkingArranger is ScatteredArranger;

        if (!isElementPasteInArrangeMode && EditMode != GraphicsEditMode.Draw)
        {
            await ChangeEditModeAsync(GraphicsEditMode.Draw);
            if (EditMode != GraphicsEditMode.Draw)
                return;
        }

        CancelOverlay();

        var paste = new ArrangerPaste(_clipboard, SnapMode);

        if (IsDrawMode && IsDrawClipActive && DrawClipRect is { } clip)
            paste.Rect.MoveTo(clip.SnappedLeft, clip.SnappedTop);
        else
            paste.Rect.MoveTo(0, 0);

        Paste = paste;
        PendingOperationMessage = "Press [Enter] to Apply Paste or [Esc] to Cancel";
        InvalidateEditor(InvalidationLevel.Overlay);
    }

    [RelayCommand]
    public void CancelOverlay()
    {
        Selection = new ArrangerSelection(WorkingArranger, SnapMode);
        Paste = null;
        ActivityMessage = string.Empty;
        PendingOperationMessage = string.Empty;

        OnPropertyChanged(nameof(CanSetDrawClipFromSelection));
        OnPropertyChanged(nameof(CanAddSelectionAsScatteredArranger));
        NotifySelectionStateChanged();
        InvalidateEditor(InvalidationLevel.Overlay);
    }

    [RelayCommand]
    public void ClearDrawClipRect()
    {
        DrawClipRect = null;
        IsDrawClipActive = false;
        OnPropertyChanged(nameof(HasDrawClipRect));
        InvalidateEditor(InvalidationLevel.Overlay);
    }

    public bool CanSetDrawClipFromSelection => IsDrawMode && Selection.HasSelection;

    [RelayCommand]
    public void SetDrawClipFromSelection()
    {
        if (!Selection.HasSelection)
            return;

        DrawClipRect = new SnappedRectangle(Selection.SelectionRect);
        IsDrawClipActive = true;
        OnPropertyChanged(nameof(HasDrawClipRect));

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
        (x, y) = ClampToSelectionBounds(x, y);
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
        (x, y) = ClampToSelectionBounds(x, y);
        Selection.StartSelection(x, y);
        IsSelecting = true;
        return true;
    }

    public void UpdateSelection(double x, double y)
    {
        if (IsSelecting)
        {
            (x, y) = ClampToSelectionBounds(x, y);
            Selection.UpdateSelectionEndpoint(x, y);
            InvalidateEditor(InvalidationLevel.Overlay);
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
            OnPropertyChanged(nameof(CanSetDrawClipFromSelection));
            OnPropertyChanged(nameof(CanAddSelectionAsScatteredArranger));
            NotifySelectionStateChanged();
            InvalidateEditor(InvalidationLevel.Overlay);
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
        var handleSize = _handleScreenSize / Math.Max(Zoom, 0.01);
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

        (x, y) = ClampToSelectionBounds(x, y);
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

        InvalidateEditor(InvalidationLevel.Overlay);
    }

    internal void ClampPastePositionToDrawClip()
    {
        if (Paste is null || !IsDrawMode || !IsDrawClipActive || DrawClipRect is not { } clip)
            return;

        var rect = Paste.Rect;
        int x = Math.Clamp(rect.SnappedLeft, clip.SnappedLeft - rect.SnappedWidth + 1, clip.SnappedRight - 1);
        int y = Math.Clamp(rect.SnappedTop, clip.SnappedTop - rect.SnappedHeight + 1, clip.SnappedBottom - 1);

        if (x != rect.SnappedLeft || y != rect.SnappedTop)
            rect.MoveTo(x, y);
    }

    private (double x, double y) ClampToSelectionBounds(double x, double y)
    {
        double minX = 0, minY = 0;
        double maxX = WorkingArranger.ArrangerPixelSize.Width;
        double maxY = WorkingArranger.ArrangerPixelSize.Height;

        if (IsDrawMode && IsDrawClipActive && DrawClipRect is { } clip)
        {
            minX = clip.SnappedLeft;
            minY = clip.SnappedTop;
            maxX = clip.SnappedRight;
            maxY = clip.SnappedBottom;
        }

        return (Math.Clamp(x, minX, maxX), Math.Clamp(y, minY, maxY));
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
            OnPropertyChanged(nameof(CanSetDrawClipFromSelection));
            OnPropertyChanged(nameof(CanAddSelectionAsScatteredArranger));
            NotifySelectionStateChanged();
            InvalidateEditor(InvalidationLevel.Overlay);
        }
    }
}
