using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using TileShop.UI.ViewModels;

namespace TileShop.UI.DragDrop;

/// <summary>
/// Drops a project tree node onto another node to move it there, using the same rules as "Move to Folder..."
/// </summary>
public sealed class TreeViewItemResourceNodeDropHandler : DropHandlerBase
{
    private const string DropReadyClass = "dropReady";

    private static ProjectTreeViewModel? FindTree(object? sender) =>
        (sender as Control)?.FindAncestorOfType<TreeView>()?.DataContext as ProjectTreeViewModel;

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state) =>
        sourceContext is ResourceNodeViewModel source
        && targetContext is ResourceNodeViewModel target
        && FindTree(sender)?.CanDropNode(source, target) is true;

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (sourceContext is not ResourceNodeViewModel source || targetContext is not ResourceNodeViewModel target
            || FindTree(sender) is not { } tree || !tree.CanDropNode(source, target))
        {
            return false;
        }

        _ = tree.MoveNodeToAsync(source.Node, target.Node);
        return true;
    }

    public override void Enter(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        UpdateEffects(sender, e, sourceContext, targetContext);

        if (e.DragEffects != DragDropEffects.None && sender is Control control)
            control.Classes.Add(DropReadyClass);
    }

    public override void Over(object? sender, DragEventArgs e, object? sourceContext, object? targetContext) =>
        UpdateEffects(sender, e, sourceContext, targetContext);

    public override void Drop(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        // No DragLeave is raised after a drop, so the highlight has to be cleared here
        (sender as Control)?.Classes.Remove(DropReadyClass);

        if (!Execute(sender, e, sourceContext, targetContext, null))
            e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    public override void Leave(object? sender, RoutedEventArgs e)
    {
        (sender as Control)?.Classes.Remove(DropReadyClass);
        base.Leave(sender, e);
    }

    private void UpdateEffects(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        e.DragEffects = Validate(sender, e, sourceContext, targetContext, null) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }
}
