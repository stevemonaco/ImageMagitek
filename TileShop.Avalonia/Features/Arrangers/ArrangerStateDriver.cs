using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.Shared.EventModels;
using TileShop.Shared.Models;

namespace TileShop.Shared.Input;

public abstract class ArrangerStateDriver<T> : ObservableRecipient, IStateDriver
    where T : ArrangerEditorViewModel
{
    public T ViewModel { get; protected set; }
    public bool IsSelecting { get; set; }

    public ArrangerStateDriver(T ViewModel)
    {
        this.ViewModel = ViewModel;
    }

    /// <summary>
    /// A mouse button was pressed down
    /// </summary>
    /// <param name="x">x-coordinate in unzoomed pixels</param>
    /// <param name="y">y-coordinate in unzoomed pixels</param>
    /// <param name="mouseState">State of mouse and key modifiers</param>
    public virtual void MouseDown(double x, double y, MouseState mouseState)
    {
        var arranger = ViewModel.WorkingArranger;

        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);

        if (mouseState.LeftButtonPressed && ViewModel.Paste is not null && !ViewModel.Paste.Rect.ContainsPointSnapped(xc, yc))
        {
            ViewModel.ApplyPaste(ViewModel.Paste);
            ViewModel.Paste = null;
        }

        if (ViewModel.Selection?.HasSelection is true && mouseState.LeftButtonPressed && ViewModel.Selection.SelectionRect.ContainsPointSnapped(xc, yc))
        {
            // Start drag for selection (Handled by DragDrop in View)
        }
        else if (ViewModel.Paste is not null && mouseState.LeftButtonPressed && ViewModel.Paste.Rect.ContainsPointSnapped(xc, yc))
        {
            // Start drag for paste (Handled by DragDrop in View)
        }
        else if (mouseState.LeftButtonPressed)
        {
            IsSelecting = true;
            ViewModel.StartNewSelection(xc, yc);
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
            ViewModel.CompleteSelection();
        }
    }

    public virtual void MouseEnter()
    {
    }

    public virtual void MouseLeave()
    {
        var notifyEvent = new NotifyStatusEvent("", NotifyStatusDuration.Reset);
        Messenger.Send(notifyEvent);
    }

    /// <summary>
    /// The mouse has moved
    /// </summary>
    /// <param name="x">x-coordinate in unzoomed pixels</param>
    /// <param name="y">y-coordinate in unzoomed pixels</param>
    public virtual void MouseMove(double x, double y)
    {
        if (ViewModel.Selection is null)
            return;

        var arranger = ViewModel.WorkingArranger;

        if (x < 0 || y < 0 || x >= arranger.ArrangerPixelSize.Width || y >= arranger.ArrangerPixelSize.Height)
            return;

        int xc = Math.Clamp((int)x, 0, arranger.ArrangerPixelSize.Width - 1);
        int yc = Math.Clamp((int)y, 0, arranger.ArrangerPixelSize.Height - 1);

        if (IsSelecting)
            ViewModel.UpdateSelection(xc, yc);

        if (ViewModel.Selection.HasSelection)
        {
            string notifyMessage;
            var rect = ViewModel.Selection.SelectionRect;
            if (rect.SnapMode == SnapMode.Element)
                notifyMessage = $"Element Selection: {rect.SnappedWidth / arranger.ElementPixelSize.Width} x {rect.SnappedHeight / arranger.ElementPixelSize.Height}" +
                    $" at ({rect.SnappedLeft / arranger.ElementPixelSize.Width}, {rect.SnappedTop / arranger.ElementPixelSize.Height})";
            else
                notifyMessage = $"Pixel Selection: {rect.SnappedWidth} x {rect.SnappedHeight}" +
                    $" at ({rect.SnappedLeft}, {rect.SnappedTop})";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            Messenger.Send(notifyEvent);
        }
        else
        {
            var notifyMessage = $"{arranger.Name}: ({(int)Math.Truncate(x)}, {(int)Math.Truncate(y)})";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            Messenger.Send(notifyEvent);
        }
    }

    public virtual void KeyPress(KeyState keyState)
    {
    }

    public virtual void MouseWheel(MouseWheelDirection direction, KeyModifiers modifiers)
    {
    }
}
