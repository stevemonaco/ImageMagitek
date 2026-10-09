using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using ImageMagitek;
using TileShop.Shared.Models;
using TileShop.Shared.Tools;
using TileShop.UI.Controls;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.DragDrop;
public class ArrangerDropHandler : DropHandlerBase
{
    public override void Over(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        if (sourceContext is ArrangerPaste paste &&
            targetContext is GraphicsEditorViewModel targetVm &&
            sender is Control control)
        {
            if (paste.Copy is ElementCopy && !targetVm.CanAcceptElementPastes)
                return;

            if (paste.Copy is IndexedPixelCopy or DirectPixelCopy && !targetVm.CanAcceptPixelPastes)
                return;

            targetVm.Paste = paste;

            paste.SnapMode = targetVm.EditMode == GraphicsEditMode.Arrange
                ? SnapMode.Element
                : SnapMode.Pixel;

            var screenPos = e.GetPosition(control);
            var p = control is InfiniteCanvas canvas
                ? canvas.ScreenToLocalPoint(screenPos)
                : screenPos;
            targetVm.Paste.MoveTo((int)p.X, (int)p.Y);
            targetVm.ClampPastePositionToDrawClip();
            targetVm.InvalidateEditor(InvalidationLevel.Overlay);

            e.Handled = true;
        }
        else
            base.Over(sender, e, sourceContext, targetContext);
    }

    public override void Enter(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        if (targetContext is GraphicsEditorViewModel { Selection.HasSelection: true } targetVm)
        {
            targetVm.Selection = new ArrangerSelection(targetVm.WorkingArranger, targetVm.SnapMode);
            targetVm.InvalidateEditor(InvalidationLevel.Overlay);
        }

        base.Enter(sender, e, sourceContext, targetContext);
    }

    public override void Leave(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: GraphicsEditorViewModel vm } control)
        {
            vm.CancelOverlay();
        }
        base.Leave(sender, e);
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (sender is Control && sourceContext is ArrangerPaste && targetContext is GraphicsEditorViewModel)
        {
            return true;
        }
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control &&
            sourceContext is ArrangerPaste paste &&
            targetContext is GraphicsEditorViewModel targetVm &&
            sender is Control control
            && Validate(control, e, sourceContext, targetContext, state))
        {
            paste.IsDragging = false;
            _ = targetVm.DropPasteAsync(paste, e.KeyModifiers.HasFlag(KeyModifiers.Shift));

            ActivateEditorDockTab(control);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Activates the dock tab containing the drop target.
    /// Focus is handled separately by ShellView via FocusedDockableChanged.
    /// </summary>
    private static void ActivateEditorDockTab(Control control)
    {
        var dockControl = control.FindAncestorOfType<DockControl>();
        if (dockControl?.Factory is not { } factory)
            return;

        var dockableVm = control.GetVisualAncestors()
            .OfType<Control>()
            .Select(c => c.DataContext)
            .OfType<DockableEditorViewModel>()
            .FirstOrDefault();

        if (dockableVm is null)
            return;

        factory.SetActiveDockable(dockableVm);
        if (dockableVm.Owner is IDock owner)
            factory.SetFocusedDockable(owner, dockableVm);
    }
}
