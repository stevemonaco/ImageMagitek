﻿using System;
using System.Linq;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;
using TileShop.Shared.Models;
using ImageMagitek.Services;
using ImageMagitek;
using TileShop.AvaloniaUI.ViewExtenders;
using TileShop.AvaloniaUI.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using TileShop.AvaloniaUI.Models;
using CommunityToolkit.Mvvm.Input;

namespace TileShop.AvaloniaUI.ViewModels;

public enum EditMode { ArrangeGraphics, ModifyGraphics }

public abstract partial class ArrangerEditorViewModel : ResourceEditorBaseViewModel
{
    public Arranger WorkingArranger { get; protected set; }

    protected IPaletteService _paletteService;
    protected IWindowManager _windowManager;

    [ObservableProperty] private BitmapAdapter _bitmapAdapter;

    public bool IsSingleLayout => WorkingArranger?.Layout == ElementLayout.Single;
    public bool IsTiledLayout => WorkingArranger?.Layout == ElementLayout.Tiled;

    public bool IsIndexedColor => WorkingArranger?.ColorType == PixelColorType.Indexed;
    public bool IsDirectColor => WorkingArranger?.ColorType == PixelColorType.Direct;

    [ObservableProperty] protected bool _showGridlines = false;
    [ObservableProperty] protected ObservableCollection<Gridline> _gridlines;

    protected int _zoom = 1;
    public int Zoom
    {
        get => _zoom;
        set
        {
            if (SetProperty(ref _zoom, value))
                CreateGridlines();
        }
    }

    public int MinZoom => 1;
    public int MaxZoom { get; protected set; } = 16;

    public bool CanChangeSnapMode { get; protected set; }
    [ObservableProperty] protected EditMode _editMode = EditMode.ArrangeGraphics;

    public virtual bool CanEditSelection
    {
        get
        {
            if (Selection.HasSelection)
            {
                var rect = Selection.SelectionRect;
                if (rect.SnappedWidth == 0 || rect.SnappedHeight == 0)
                    return false;

                return !WorkingArranger.EnumerateElementsWithinPixelRange(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight)
                    .Any(x => x is null || x?.Source is null);
            }

            return false;
        }
    }

    private SnapMode _snapMode = SnapMode.Element;
    public SnapMode SnapMode
    {
        get => _snapMode;
        set
        {
            SetProperty(ref _snapMode, value);
            if (Selection is not null)
                Selection.SnapMode = SnapMode;
        }
    }

    [ObservableProperty] private ArrangerSelection _selection;
    [ObservableProperty] private bool _isSelecting;
    [ObservableProperty] private ArrangerPaste _paste;

    public bool CanAcceptPixelPastes { get; set; }
    public bool CanAcceptElementPastes { get; set; }

    public ArrangerEditorViewModel(IWindowManager windowManager, IPaletteService paletteService)
    {
        //_events = events;
        //_events.Subscribe(this);
        _windowManager = windowManager;
        _paletteService = paletteService;
    }

    /// <summary>
    /// Redraws the ImageBase onto the BitmapAdapter
    /// </summary>
    public abstract void Render();

    /// <summary>
    /// Applies the paste to the Editor
    /// </summary>
    /// <param name="paste"></param>
    public abstract void ApplyPaste(ArrangerPaste paste);

    /// <summary>
    /// Creates the Gridlines for an overlay using the extents and element spacing of the Working Arranger
    /// </summary>
    protected virtual void CreateGridlines()
    {
        if (WorkingArranger is not null)
            CreateGridlines(0, 0, WorkingArranger.ArrangerPixelSize.Width, WorkingArranger.ArrangerPixelSize.Height,
                WorkingArranger.ElementPixelSize.Width, WorkingArranger.ElementPixelSize.Height);
    }

    /// <summary>
    /// Creates the Gridlines for an overlay
    /// </summary>
    /// <param name="x">Starting x-coordinate in pixel coordinates, inclusive</param>
    /// <param name="y">Starting y-coordinate in pixel coordinates, inclusive</param>
    /// <param name="x2">Ending x-coordinate in pixel coordinates, inclusive</param>
    /// <param name="y2">Ending y-coordinate in pixel coordinates, inclusive</param>
    /// <param name="xSpacing">Spacing between gridlines in pixel coordinates</param>
    /// <param name="height">Spacing between gridlines in pixel coordinates</param>
    protected void CreateGridlines(int x1, int y1, int x2, int y2, int xSpacing, int ySpacing)
    {
        if (WorkingArranger is null)
            return;

        _gridlines = new();
        for (int x = x1; x <= x2; x += xSpacing) // Vertical gridlines
        {
            var gridline = new Gridline(x * Zoom + 1, 0,
                x * Zoom + 1, y2 * Zoom);
            _gridlines.Add(gridline);
        }

        for (int y = y1; y <= y2; y += ySpacing) // Horizontal gridlines
        {
            var gridline = new Gridline(0, y * Zoom + 1,
                x2 * Zoom, y * Zoom + 1);
            _gridlines.Add(gridline);
        }

        OnPropertyChanged(nameof(Gridlines));
    }

    #region Commands
    [ICommand] public virtual void ZoomIn() => Zoom = Math.Clamp(Zoom + 1, MinZoom, MaxZoom);
    [ICommand] public virtual void ZoomOut() => Zoom = Math.Clamp(Zoom - 1, MinZoom, MaxZoom);

    [ICommand] public virtual void ToggleGridlineVisibility() => ShowGridlines ^= true;

    [ICommand]
    public virtual void EditSelection()
    {
        if (!CanEditSelection)
            return;

        EditArrangerPixelsEvent editEvent;
        var rect = Selection.SelectionRect;

        if (SnapMode == SnapMode.Element && WorkingArranger.Layout == ElementLayout.Tiled)
        {
            // Clone a subsection of the arranger and show the full subarranger
            WorkingArranger.CopyElements();
            var arranger = WorkingArranger.CloneArranger(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
            editEvent = new EditArrangerPixelsEvent(arranger, Resource as Arranger, 0, 0, rect.SnappedWidth, rect.SnappedHeight);
        }
        else
        {
            // Clone the entire arranger and show a subsection of the cloned arranger
            var arranger = WorkingArranger.CloneArranger();
            editEvent = new EditArrangerPixelsEvent(arranger, Resource as Arranger, rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
        }

        //_events.PublishOnUIThread(editEvent);
        CancelOverlay();
    }

    [ICommand]
    public virtual void SelectAll()
    {
        CancelOverlay();
        Selection = new ArrangerSelection(WorkingArranger, SnapMode);
        Selection.StartSelection(0, 0);
        Selection.UpdateSelectionEndpoint(WorkingArranger.ArrangerPixelSize.Width, WorkingArranger.ArrangerPixelSize.Height);
        OnPropertyChanged(nameof(CanEditSelection));
    }

    [ICommand]
    public virtual void CancelOverlay()
    {
        Selection = new ArrangerSelection(WorkingArranger, SnapMode);
        Paste = null;

        OnPropertyChanged(nameof(CanEditSelection));
    }

    public virtual void StartNewSelection(int x, int y)
    {
        Selection.StartSelection(x, y);
        IsSelecting = true;
    }

    public virtual void UpdateSelection(int x, int y)
    {
        if (IsSelecting)
            Selection.UpdateSelectionEndpoint(x, y);
    }

    public virtual void CompleteSelection()
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
    #endregion

//    #region Mouse Actions
//    public virtual void OnMouseMove(object sender, MouseCaptureArgs e)
//    {
//        if (Selection is null)
//            return;

//        int x = Math.Clamp((int)e.X / Zoom, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
//        int y = Math.Clamp((int)e.Y / Zoom, 0, WorkingArranger.ArrangerPixelSize.Height - 1);

//        if (IsSelecting)
//            UpdateSelection(x, y);

//        if (Selection.HasSelection)
//        {
//            string notifyMessage;
//            var rect = Selection.SelectionRect;
//            if (rect.SnapMode == SnapMode.Element)
//                notifyMessage = $"Element Selection: {rect.SnappedWidth / WorkingArranger.ElementPixelSize.Width} x {rect.SnappedHeight / WorkingArranger.ElementPixelSize.Height}" +
//                    $" at ({rect.SnappedLeft / WorkingArranger.ElementPixelSize.Width}, {rect.SnappedRight / WorkingArranger.ElementPixelSize.Height})";
//            else
//                notifyMessage = $"Pixel Selection: {rect.SnappedWidth} x {rect.SnappedHeight}" +
//                    $" at ({rect.SnappedLeft}, {rect.SnappedTop})";
//            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
//            _events.PublishOnUIThread(notifyEvent);
//        }
//        else
//        {
//            var notifyMessage = $"{WorkingArranger.Name}: ({(int)Math.Truncate(e.X / Zoom)}, {(int)Math.Truncate(e.Y / Zoom)})";
//            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
//            _events.PublishOnUIThread(notifyEvent);
//        }
//    }

//    public virtual void OnMouseLeave(object sender, MouseCaptureArgs e)
//    {
//        var notifyEvent = new NotifyStatusEvent("", NotifyStatusDuration.Indefinite);
//        _events.PublishOnUIThread(notifyEvent);
//    }

//    public virtual void OnMouseUp(object sender, MouseCaptureArgs e)
//    {
//        if (IsSelecting)
//        {
//            CompleteSelection();
//        }
//    }

//    public virtual void OnMouseDown(object sender, MouseCaptureArgs e)
//    {
//        int x = Math.Clamp((int)e.X / Zoom, 0, WorkingArranger.ArrangerPixelSize.Width - 1);
//        int y = Math.Clamp((int)e.Y / Zoom, 0, WorkingArranger.ArrangerPixelSize.Height - 1);

//        if (e.LeftButton && Paste is not null && !Paste.Rect.ContainsPointSnapped(x, y))
//        {
//            ApplyPaste(Paste);
//            Paste = null;
//        }

//        if (Selection?.HasSelection is true && e.LeftButton && Selection.SelectionRect.ContainsPointSnapped(x, y))
//        {
//            // Start drag for selection (Handled by DragDrop in View)
//        }
//        else if (Paste is not null && e.LeftButton && Paste.Rect.ContainsPointSnapped(x, y))
//        {
//            // Start drag for paste (Handled by DragDrop in View)
//        }
//        else if (e.LeftButton)
//        {
//            StartNewSelection(x, y);
//        }
//    }

//    public virtual void OnMouseWheel(object sender, MouseCaptureArgs e)
//    {
//        if (e.WheelDirection == MouseWheelDirection.Up)
//            ZoomIn();
//        else if (e.WheelDirection == MouseWheelDirection.Down)
//            ZoomOut();
//    }

//#pragma warning disable CS0067
//    // Unused events that are required to be present for IMouseCaptureProxy
//    public virtual event EventHandler Capture;
//    public virtual event EventHandler Release;
//#pragma warning restore CS0067
//    #endregion

//    #region Drag and Drop Implementation
//    public virtual void Drop(IDropInfo dropInfo)
//    {
//        if (dropInfo.Data is ArrangerPaste paste)
//        {
//            paste.SnapMode = SnapMode;
//            Paste = paste;
//            Paste.MoveTo((int)dropInfo.DropPosition.X, (int)dropInfo.DropPosition.Y);
//        }
//    }

//    public virtual void DragOver(IDropInfo dropInfo)
//    {
//        if (dropInfo.Data is ArrangerPaste paste)
//        {
//            if (paste.Copy is ElementCopy && !CanAcceptElementPastes)
//                return;

//            if ((paste.Copy is IndexedPixelCopy || paste.Copy is DirectPixelCopy) && !CanAcceptPixelPastes)
//                return;

//            if (!ReferenceEquals(dropInfo.DragInfo.SourceItem, this))
//                (dropInfo.DragInfo.SourceItem as ArrangerEditorViewModel).CancelOverlay();

//            if (Paste != paste)
//            {
//                Paste = new ArrangerPaste(paste.Copy, SnapMode)
//                {
//                    DeltaX = paste.DeltaX,
//                    DeltaY = paste.DeltaY
//                };
//            }

//            Paste.MoveTo((int)dropInfo.DropPosition.X, (int)dropInfo.DropPosition.Y);
//            dropInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;
//        }
//    }

//    public virtual void StartDrag(IDragInfo dragInfo)
//    {
//        if (Selection.HasSelection)
//        {
//            var rect = Selection.SelectionRect;

//            ArrangerCopy copy = default;
//            if (SnapMode == SnapMode.Element)
//            {
//                int x = rect.SnappedLeft / WorkingArranger.ElementPixelSize.Width;
//                int y = rect.SnappedTop / WorkingArranger.ElementPixelSize.Height;
//                int width = rect.SnappedWidth / WorkingArranger.ElementPixelSize.Width;
//                int height = rect.SnappedHeight / WorkingArranger.ElementPixelSize.Height;
//                copy = WorkingArranger.CopyElements(x, y, width, height);
//                (copy as ElementCopy).ProjectResource = OriginatingProjectResource;
//            }
//            else if (SnapMode == SnapMode.Pixel && WorkingArranger.ColorType == PixelColorType.Indexed)
//            {
//                copy = WorkingArranger.CopyPixelsIndexed(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
//            }
//            else if (SnapMode == SnapMode.Pixel && WorkingArranger.ColorType == PixelColorType.Direct)
//            {
//                copy = WorkingArranger.CopyPixelsDirect(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
//            }

//            var paste = new ArrangerPaste(copy, SnapMode)
//            {
//                DeltaX = (int)dragInfo.DragStartPosition.X - Selection.SelectionRect.SnappedLeft,
//                DeltaY = (int)dragInfo.DragStartPosition.Y - Selection.SelectionRect.SnappedTop
//            };
//            dragInfo.Data = paste;
//            dragInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;

//            Selection = new ArrangerSelection(WorkingArranger, SnapMode);
//        }
//        else if (Paste is not null)
//        {
//            Paste.DeltaX = (int)dragInfo.DragStartPosition.X - Paste.Rect.SnappedLeft;
//            Paste.DeltaY = (int)dragInfo.DragStartPosition.Y - Paste.Rect.SnappedTop;
//            Paste.SnapMode = SnapMode;

//            dragInfo.Data = Paste;
//            dragInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;
//        }
//    }

//    public virtual bool CanStartDrag(IDragInfo dragInfo)
//    {
//        if (Selection.HasSelection)
//        {
//            return Selection.SelectionRect.ContainsPointSnapped(dragInfo.DragStartPosition.X, dragInfo.DragStartPosition.Y);
//        }
//        else if (Paste is not null)
//        {
//            return Paste.Rect.ContainsPointSnapped(dragInfo.DragStartPosition.X, dragInfo.DragStartPosition.Y);
//        }
//        else
//            return false;
//    }

//    public virtual void Dropped(IDropInfo dropInfo) { }

//    public virtual void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo) { }

//    public virtual void DragCancelled()
//    {
//        CancelOverlay();
//    }
//    public virtual bool TryCatchOccurredException(Exception exception) => false;
//    #endregion
}
