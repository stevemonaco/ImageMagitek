using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using TileShop.AvaloniaUI.Models;
using TileShop.AvaloniaUI.ViewModels;

namespace TileShop.AvaloniaUI.DragDrop;

public sealed class ListBoxRemappableColorDropHandler : DropHandlerBase
{
    private bool Validate<T>(ListBoxItem listBoxItem, DragEventArgs e, object? sourceContext, object? targetContext, bool bExecute)
        where T : RemappableColorModel
    {
        if (sourceContext is not T sourceItem
            || targetContext is not ColorRemapViewModel vm
            || listBoxItem.GetVisualAt(e.GetPosition(listBoxItem)) is not IControl targetControl
            || targetControl.DataContext is not T targetItem)
        {
            return false;
        }

        var items = vm.FinalColors;
        var sourceIndex = sourceItem.Index;
        var targetIndex = items.IndexOf(targetItem);

        if (sourceIndex < 0 || targetIndex < 0)
        {
            return false;
        }

        if (bExecute)
        {
            items[targetIndex] = new RemappableColorModel(sourceItem.Color, sourceItem.Index);
        }

        return true;
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is IControl && sender is ListBoxItem listBoxItem)
        {
            return Validate<RemappableColorModel>(listBoxItem, e, sourceContext, targetContext, false);
        }
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is IControl && sender is ListBoxItem listBoxItem)
        {
            return Validate<RemappableColorModel>(listBoxItem, e, sourceContext, targetContext, true);
        }
        return false;
    }
}