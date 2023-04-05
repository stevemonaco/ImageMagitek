using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using TileShop.UI.Models;
using TileShop.UI.ViewModels;

namespace TileShop.UI.ViewExtenders.DragDrop;
public sealed class TreeViewItemResourceNodeDropHandler : DropHandlerBase
{
    private bool Validate<T>(Control control, DragEventArgs e, object? sourceContext, object? targetContext, bool bExecute)
        where T : ResourceNodeViewModel
    {
        if (sourceContext is not T sourceItem
            || targetContext is not ResourceNodeViewModel vm
            || control.GetVisualAt(e.GetPosition(control)) is not Control targetControl
            || targetControl.DataContext is not T targetItem)
        {
            return false;
        }

        //var items = vm.FinalColors;
        //var sourceIndex = sourceItem.Index;
        //var targetIndex = items.IndexOf(targetItem);

        //if (sourceIndex < 0 || targetIndex < 0)
        //{
        //    return false;
        //}

        //if (bExecute)
        //{
        //    items[targetIndex] = new RemappableColorModel(sourceItem.Color, sourceItem.Index);
        //}

        return true;
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is Control control)
        {
            return Validate<ResourceNodeViewModel>(control, e, sourceContext, targetContext, false);
        }
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is Control control)
        {
            return Validate<ResourceNodeViewModel>(control, e, sourceContext, targetContext, true);
        }
        return false;
    }

    public override void Enter(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        if (sender is not Control control)
            return;

        if (Validate(sender, e, sourceContext, targetContext, null) == false)
        {
            e.DragEffects = DragDropEffects.None;
        }
        else
        {
            control.Classes.Add("dropReady");
            e.DragEffects |= DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link;
        }

        e.Handled = true;
    }

    public override void Over(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
    }

    public override void Leave(object? sender, RoutedEventArgs e)
    {
        if (sender is Border control && control.Classes.Contains("colorDrop") && !control.IsPointerOver)
        {
            control.Classes.Remove("dropReady");
        }

        base.Leave(sender, e);
    }
}
