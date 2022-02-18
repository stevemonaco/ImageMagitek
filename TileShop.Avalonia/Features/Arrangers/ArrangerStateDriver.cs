using System;
using CommunityToolkit.Mvvm.Messaging;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.Shared.EventModels;
using TileShop.Shared.Models;

namespace TileShop.Shared.Input;

public abstract class ArrangerStateDriver<T> : IStateDriver
    where T : ArrangerEditorViewModel
{
    private T _viewModel;

    public bool IsSelecting { get; set; }

    public ArrangerStateDriver(T ViewModel)
    {
        _viewModel = ViewModel;
    }

    /// <summary>
    /// A mouse button was pressed down
    /// </summary>
    /// <param name="x">x-coordinate in unzoomed pixels</param>
    /// <param name="y">y-coordinate in unzoomed pixels</param>
    /// <param name="mouseState">State of mouse and key modifiers</param>
    public virtual void MouseDown(double x, double y, MouseState mouseState)
    {
        var zoom = _viewModel.Zoom;
        var arranger = _viewModel.WorkingArranger;

        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);

        if (mouseState.LeftButtonPressed && _viewModel.Paste is not null && !_viewModel.Paste.Rect.ContainsPointSnapped(xc, yc))
        {
            _viewModel.ApplyPaste(_viewModel.Paste);
            _viewModel.Paste = null;
        }

        if (_viewModel.Selection?.HasSelection is true && mouseState.LeftButtonPressed && _viewModel.Selection.SelectionRect.ContainsPointSnapped(xc, yc))
        {
            // Start drag for selection (Handled by DragDrop in View)
        }
        else if (_viewModel.Paste is not null && mouseState.LeftButtonPressed && _viewModel.Paste.Rect.ContainsPointSnapped(xc, yc))
        {
            // Start drag for paste (Handled by DragDrop in View)
        }
        else if (mouseState.LeftButtonPressed)
        {
            IsSelecting = true;
            _viewModel.StartNewSelection(xc, yc);
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
            _viewModel.CompleteSelection();
        }
    }

    public virtual void MouseEnter()
    {
    }

    public virtual void MouseLeave()
    {
        var notifyEvent = new NotifyStatusEvent("", NotifyStatusDuration.Indefinite);
        WeakReferenceMessenger.Default.Send(notifyEvent);
    }

    /// <summary>
    /// The mouse has moved
    /// </summary>
    /// <param name="x">x-coordinate in unzoomed pixels</param>
    /// <param name="y">y-coordinate in unzoomed pixels</param>
    public virtual void MouseMove(double x, double y)
    {
        if (_viewModel.Selection is null)
            return;

        var arranger = _viewModel.WorkingArranger;

        if (x < 0 || y < 0 || x >= arranger.ArrangerPixelSize.Width || y >= arranger.ArrangerPixelSize.Height)
            return;

        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);

        if (IsSelecting)
            _viewModel.UpdateSelection(xc, yc);

        if (_viewModel.Selection.HasSelection)
        {
            string notifyMessage;
            var rect = _viewModel.Selection.SelectionRect;
            if (rect.SnapMode == SnapMode.Element)
                notifyMessage = $"Element Selection: {rect.SnappedWidth / arranger.ElementPixelSize.Width} x {rect.SnappedHeight / arranger.ElementPixelSize.Height}" +
                    $" at ({rect.SnappedLeft / arranger.ElementPixelSize.Width}, {rect.SnappedTop / arranger.ElementPixelSize.Height})";
            else
                notifyMessage = $"Pixel Selection: {rect.SnappedWidth} x {rect.SnappedHeight}" +
                    $" at ({rect.SnappedLeft}, {rect.SnappedTop})";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            WeakReferenceMessenger.Default.Send(notifyEvent);
        }
        else
        {
            var notifyMessage = $"{arranger.Name}: ({(int)Math.Truncate(x)}, {(int)Math.Truncate(y)})";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            WeakReferenceMessenger.Default.Send(notifyEvent);
        }
    }

    public virtual void KeyPress(KeyState keyState)
    {

    }

    public virtual void MouseWheel(MouseWheelDirection direction, KeyModifiers modifiers)
    {
        if (direction == MouseWheelDirection.Up && modifiers.HasFlag(KeyModifiers.Ctrl))
            _viewModel.ZoomIn();
        else if (direction == MouseWheelDirection.Down && modifiers.HasFlag(KeyModifiers.Ctrl))
            _viewModel.ZoomOut();
    }
}
