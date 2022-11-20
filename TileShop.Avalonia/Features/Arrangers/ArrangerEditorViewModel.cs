using System;
using System.Linq;
using System.Drawing;
using TileShop.Shared.EventModels;
using TileShop.Shared.Models;
using ImageMagitek.Services;
using ImageMagitek;
using TileShop.AvaloniaUI.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using TileShop.AvaloniaUI.Models;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TileShop.Shared.Input;
using TileShop.Shared.Interactions;

using Key = TileShop.Shared.Input.Key;
using KeyModifiers = TileShop.Shared.Input.KeyModifiers;

namespace TileShop.AvaloniaUI.ViewModels;

public enum EditMode { ArrangeGraphics, ModifyGraphics }

public abstract partial class ArrangerEditorViewModel : ResourceEditorBaseViewModel, IStateDriver
{
    public Arranger WorkingArranger { get; protected set; }
    public bool CanChangeSnapMode { get; protected set; }
    public Point? LastMousePosition { get; protected set; }

    protected IPaletteService _paletteService;
    protected IInteractionService _interactions;

    [ObservableProperty] private BitmapAdapter _bitmapAdapter = null!;

    public bool IsSingleLayout => WorkingArranger?.Layout == ElementLayout.Single;
    public bool IsTiledLayout => WorkingArranger?.Layout == ElementLayout.Tiled;

    public bool IsIndexedColor => WorkingArranger?.ColorType == PixelColorType.Indexed;
    public bool IsDirectColor => WorkingArranger?.ColorType == PixelColorType.Direct;

    [ObservableProperty] protected bool _showGridlines = false;
    [ObservableProperty] protected ObservableCollection<Gridline> _gridlines = new();

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
        }
    }

    [ObservableProperty] private ArrangerSelection _selection;
    [ObservableProperty] private bool _isSelecting;
    [ObservableProperty] private ArrangerPaste? _paste;

    public bool CanAcceptPixelPastes { get; set; }
    public bool CanAcceptElementPastes { get; set; }
    public Key PrimaryAltKey { get; protected set; } = Key.LeftAlt;
    public Key SecondaryAltKey { get; protected set; } = Key.LeftShift;

    public Action? OnImageModified { get; set; }

    public ArrangerEditorViewModel(Arranger arranger, IInteractionService interactionService, IPaletteService paletteService) : base(arranger)
    {
        WorkingArranger = arranger.CloneArranger();
        Resource = arranger;
        DisplayName = WorkingArranger.Name ?? "Unnamed Arranger";
        _selection = new ArrangerSelection(arranger, SnapMode.Pixel);

        _interactions = interactionService;
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
            var gridline = new Gridline(x, 0, x, y2);
            _gridlines.Add(gridline);
        }

        for (int y = y1; y <= y2; y += ySpacing) // Horizontal gridlines
        {
            var gridline = new Gridline(0, y, x2, y);
            _gridlines.Add(gridline);
        }

        OnPropertyChanged(nameof(Gridlines));
    }

    /// <summary>
    /// A mouse button was pressed down
    /// </summary>
    /// <param name="x">x-coordinate in unzoomed pixels</param>
    /// <param name="y">y-coordinate in unzoomed pixels</param>
    /// <param name="mouseState">State of mouse and key modifiers</param>
    public virtual void MouseDown(double x, double y, MouseState mouseState)
    {
        var arranger = WorkingArranger;

        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);

        if (mouseState.LeftButtonPressed && Paste is not null && !Paste.Rect.ContainsPointSnapped(xc, yc))
        {
            ApplyPaste(Paste);
            Paste = null;
        }

        if (Selection?.HasSelection is true && mouseState.LeftButtonPressed && Selection.SelectionRect.ContainsPointSnapped(xc, yc))
        {
            // Start drag for selection (Handled by DragDrop in View)
        }
        else if (Paste is not null && mouseState.LeftButtonPressed && Paste.Rect.ContainsPointSnapped(xc, yc))
        {
            // Start drag for paste (Handled by DragDrop in View)
        }
        else if (mouseState.LeftButtonPressed && mouseState.Modifiers.HasFlag(KeyModifiers.Control))
        {
            IsSelecting = true;
            StartNewSelection(x, y);
            CompleteSelection();
        }
        else if (mouseState.LeftButtonPressed)
        {
            IsSelecting = true;
            StartNewSelection(x, y);
        }
    }

    /// <summary>
    /// A mouse button was released
    /// </summary>
    /// <param name="x">x-coordinate in unzoomed pixels</param>
    /// <param name="y">y-coordinate in unzoomed pixels</param>
    /// <param name="mouseState">State of mouse and key modifiers</param>
    public virtual void MouseUp(double x, double y, MouseState mouseState)
    {
        if (IsSelecting && mouseState.LeftButtonPressed == false)
        {
            CompleteSelection();
        }
    }

    public virtual void MouseEnter()
    {
    }

    public virtual void MouseLeave()
    {
        LastMousePosition = null;
        ActivityMessage = string.Empty;
    }

    /// <summary>
    /// The mouse has moved
    /// </summary>
    /// <param name="x">x-coordinate in unzoomed pixels</param>
    /// <param name="y">y-coordinate in unzoomed pixels</param>
    public virtual void MouseMove(double x, double y, MouseState mouseState)
    {
        var arranger = WorkingArranger;

        if (x < 0 || y < 0 || x >= arranger.ArrangerPixelSize.Width || y >= arranger.ArrangerPixelSize.Height)
        {
            LastMousePosition = null;
            return;
        }

        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);

        LastMousePosition = new(xc, yc);

        if (IsSelecting)
            UpdateSelection(x, y);

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

    public virtual void KeyPress(KeyState keyState, double? x, double? y)
    {
    }

    public virtual void KeyUp(KeyState keyState, double? x, double? y)
    {
    }

    public virtual void MouseWheel(MouseWheelDirection direction, KeyModifiers modifiers)
    {
    }

    #region Commands
    [RelayCommand] public virtual void ToggleGridlineVisibility() => ShowGridlines ^= true;

    [RelayCommand]
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
            editEvent = new EditArrangerPixelsEvent(arranger, (Arranger)Resource, 0, 0, rect.SnappedWidth, rect.SnappedHeight);
        }
        else
        {
            // Clone the entire arranger and show a subsection of the cloned arranger
            var arranger = WorkingArranger.CloneArranger();
            editEvent = new EditArrangerPixelsEvent(arranger, (Arranger)Resource, rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
        }

        WeakReferenceMessenger.Default.Send(editEvent);
        CancelOverlay();
    }

    [RelayCommand]
    public virtual void SelectAll()
    {
        CancelOverlay();
        Selection = new ArrangerSelection(WorkingArranger, SnapMode);
        Selection.StartSelection(0, 0);
        Selection.UpdateSelectionEndpoint(WorkingArranger.ArrangerPixelSize.Width, WorkingArranger.ArrangerPixelSize.Height);
        CompleteSelection();
        OnPropertyChanged(nameof(CanEditSelection));
    }

    [RelayCommand]
    public virtual void CancelOverlay()
    {
        Selection = new ArrangerSelection(WorkingArranger, SnapMode);
        Paste = null;
        ActivityMessage = string.Empty;
        PendingOperationMessage = string.Empty;

        OnPropertyChanged(nameof(CanEditSelection));
    }

    public virtual void StartNewSelection(double x, double y)
    {
        Selection.StartSelection(x, y);
        IsSelecting = true;
    }

    public virtual bool TryStartNewSingleSelection(double x, double y)
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

    public virtual void UpdateSelection(double x, double y)
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

    public virtual void CompletePaste()
    {
        if (Paste is not null)
        {
            ApplyPaste(Paste);
        }
    }
    #endregion
}
