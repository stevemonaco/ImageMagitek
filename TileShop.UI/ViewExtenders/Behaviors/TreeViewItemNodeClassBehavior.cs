using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using ImageMagitek.Project.Serialization;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Behaviors;

/// <summary>
/// Attached behavior that applies a style class to a TreeViewItem based on its DataContext type.
/// When enabled, monitors DataContext changes and sets "project", "folder", "standalone", or the node's resource kind element name.
/// </summary>
public class TreeViewItemNodeClassBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TreeViewItemNodeClassBehavior, TreeViewItem, bool>("IsEnabled");

    private static readonly string[] _allClasses =
        ["project", "folder", "standalone", .. ResourceKindRegistry.BuiltIn.Kinds.Select(x => x.ElementName)];

    static TreeViewItemNodeClassBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<TreeViewItem>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(TreeViewItem element) => element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(TreeViewItem element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(TreeViewItem item, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            item.DataContextChanged += OnDataContextChanged;
            ApplyClass(item);
        }
        else
        {
            item.DataContextChanged -= OnDataContextChanged;
            RemoveAllClasses(item);
        }
    }

    private static void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is TreeViewItem item)
        {
            ApplyClass(item);
        }
    }

    private static void ApplyClass(TreeViewItem item)
    {
        RemoveAllClasses(item);

        if (FindClass(item.DataContext) is { } className)
        {
            item.Classes.Add(className);
        }
    }

    private static string? FindClass(object? dataContext) => dataContext switch
    {
        ProjectNodeViewModel => "project",
        FolderNodeViewModel => "folder",
        StandaloneFileNodeViewModel => "standalone",
        ResourceNodeViewModel vm => ResourceKindRegistry.BuiltIn.FindByNode(vm.Node)?.ElementName,
        _ => null
    };

    private static void RemoveAllClasses(TreeViewItem item)
    {
        foreach (var className in _allClasses)
        {
            item.Classes.Remove(className);
        }
    }
}
