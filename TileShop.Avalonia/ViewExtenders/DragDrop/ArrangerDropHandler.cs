using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactions.DragAndDrop;
using ImageMagitek;
using TileShop.AvaloniaUI.Models;
using TileShop.AvaloniaUI.ViewModels;
using TileShop.AvaloniaUI.Views;

namespace TileShop.AvaloniaUI.DragDrop;
public class ArrangerDropHandler : DropHandlerBase
{
    public override void Over(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        if (sourceContext is ArrangerPaste paste &&
            targetContext is ArrangerEditorViewModel targetVm &&
            sender is IControl control)
        {
            if (paste.Copy is ElementCopy && !targetVm.CanAcceptElementPastes)
                return;

            if ((paste.Copy is IndexedPixelCopy || paste.Copy is DirectPixelCopy) && !targetVm.CanAcceptPixelPastes)
                return;

            if (targetVm.Paste != paste)
            {
                targetVm.Paste = new ArrangerPaste(paste.Copy, targetVm.SnapMode)
                {
                    DeltaX = paste.DeltaX,
                    DeltaY = paste.DeltaY
                };
            }

            var p = e.GetPosition(control);
            targetVm.Paste.MoveTo((int)p.X, (int)p.Y);

            //Debug.WriteLine($"Move: {control.Name}");
            e.Handled = true;
        }
        else
            base.Over(sender, e, sourceContext, targetContext);
    }

    public override void Enter(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        if (sender is IControl control) // && control.DataContext is ArrangerEditorViewModel vm)
        {
            Debug.WriteLine($"Entering: {control.Name}");
        }
        base.Enter(sender, e, sourceContext, targetContext);
    }

    public override void Leave(object? sender, RoutedEventArgs e)
    {
        if (sender is IControl { DataContext: ArrangerEditorViewModel vm } control)
        {
            Debug.WriteLine($"Leaving Drop: {control.Name}");

            if (!vm.LastMousePosition.HasValue)
            {
                vm.CancelOverlay();
                //if (!control.Bounds.Contains(new Avalonia.Point(driver.LastMouseX.Value, driver.LastMouseY.Value)))
                //{
                //    Debug.WriteLine($"Leaving: {control.Name}");
                //    vm.CancelOverlay();
                //}
            }
        }
        base.Leave(sender, e);
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (sender is IControl && sourceContext is ArrangerPaste && targetContext is ArrangerEditorViewModel)
        {
            return true;
        }
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is IControl &&
            sourceContext is ArrangerPaste &&
            targetContext is ArrangerEditorViewModel targetVm &&
            sender is IControl control
            && Validate(control, e, sourceContext, targetContext, state))
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                targetVm.CompletePaste();
            }
            else
            {
                targetVm.PendingOperationMessage = $"Press [Enter] to Apply Paste or [Esc] to Cancel";
            }

            control.Focus();
            return true;
        }

        return false;
    }
}
