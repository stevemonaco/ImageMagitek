using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.DragAndDrop;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using TileShop.AvaloniaUI.Models;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.Shared.EventModels;

namespace TileShop.AvaloniaUI.DragDrop;
public class ArrangerPasteDropHandler : DropHandlerBase
{
    private bool Validate<T>(Canvas canvas, DragEventArgs e, object? sourceContext, object? targetContext)
        where T : ArrangerEditorViewModel
    {
        if (sourceContext is not T sourceItem
            || targetContext is not T vm
            //|| listBoxItem.GetVisualAt(e.GetPosition(listBoxItem)) is not IControl targetControl
            //|| targetControl.DataContext is not T targetItem)
            )
        {
            return false;
        }

        return true;
    }

    public override void Over(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        if (sourceContext is ArrangerEditorViewModel sourceVm &&
            targetContext is ArrangerEditorViewModel targetVm &&
            sourceVm.Paste is not null &&
            sender is IControl control)
        {
            if (sourceVm.Paste.Copy is ElementCopy && !targetVm.CanAcceptElementPastes)
                return;

            if ((sourceVm.Paste.Copy is IndexedPixelCopy || sourceVm.Paste.Copy is DirectPixelCopy) && !targetVm.CanAcceptPixelPastes)
                return;

            if (targetVm.Paste != sourceVm.Paste)
            {
                targetVm.Paste = new ArrangerPaste(sourceVm.Paste.Copy, targetVm.SnapMode)
                {
                    DeltaX = sourceVm.Paste.DeltaX,
                    DeltaY = sourceVm.Paste.DeltaY
                };
            }

            var p = e.GetPosition(control);
            targetVm.Paste.MoveTo((int)p.X, (int)p.Y);

            e.Handled = true;
        }
        else
            base.Over(sender, e, sourceContext, targetContext);
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is IControl && sender is Canvas canvas)
        {
            return Validate<ArrangerEditorViewModel>(canvas, e, sourceContext, targetContext);
        }
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is IControl &&
            sourceContext is ArrangerEditorViewModel sourceVm &&
            targetContext is ArrangerEditorViewModel targetVm &&
            sender is Canvas canvas
            && Validate<ArrangerEditorViewModel>(canvas, e, sourceContext, targetContext))
        {
            if (!ReferenceEquals(sourceVm, targetVm))
                sourceVm.CancelOverlay();

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                targetVm.CompletePaste();
            }
            else
            {
                var notifyMessage = $"Press [Enter] to Apply Paste or [Esc] to Cancel";
                var notifyEvent = new NotifyOperationEvent(notifyMessage);
                WeakReferenceMessenger.Default.Send(notifyEvent);
            }

            canvas.Focus();

            //if (dropInfo.Data is ArrangerPaste paste)
            //{
            //    paste.SnapMode = SnapMode;
            //    Paste = paste;
            //    Paste.MoveTo((int)dropInfo.DropPosition.X, (int)dropInfo.DropPosition.Y);
            //}

            return true;
        }
        return false;
    }
}
