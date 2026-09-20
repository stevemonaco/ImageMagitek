using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactions.DragAndDrop;
using TileShop.UI.Models;

namespace TileShop.UI.DragDrop;

/// <summary>
/// Drops a palette color onto another to remap the target's pixels to the dragged color
/// </summary>
public sealed class RemappableColorDropHandler : DropHandlerBase
{
    private const string DropReadyClass = "dropReady";

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state) =>
        sourceContext is RemappableColorModel && targetContext is RemappableColorModel;

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (sourceContext is not RemappableColorModel source || targetContext is not RemappableColorModel target)
            return false;

        target.RemapTo(source);
        return true;
    }

    public override void Enter(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        base.Enter(sender, e, sourceContext, targetContext);

        if (e.DragEffects != DragDropEffects.None && sender is Control control)
            control.Classes.Add(DropReadyClass);
    }

    public override void Drop(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        // No DragLeave is raised after a drop, so the highlight has to be cleared here
        (sender as Control)?.Classes.Remove(DropReadyClass);
        base.Drop(sender, e, sourceContext, targetContext);
    }

    public override void Leave(object? sender, RoutedEventArgs e)
    {
        (sender as Control)?.Classes.Remove(DropReadyClass);
        base.Leave(sender, e);
    }
}
