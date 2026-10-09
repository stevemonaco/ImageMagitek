using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using Avalonia;
using Avalonia.Controls;
using TileShop.UI.ViewModels;

namespace TileShop.UI.Behaviors;

/// <summary>
/// Attached behavior that applies a style class to a TreeViewItem based on its DataContext type.
/// When enabled, monitors DataContext changes and sets a class like "project", "folder", "datafile", "arranger", or "palette".
/// </summary>
public class TreeViewItemNodeClassBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TreeViewItemNodeClassBehavior, TreeViewItem, bool>("IsEnabled");

    private static readonly FrozenDictionary<Type, string> _nodeClassMap = new Dictionary<Type, string>
    {
        [typeof(ProjectNodeViewModel)] = "project",
        [typeof(FolderNodeViewModel)] = "folder",
        [typeof(DataFileNodeViewModel)] = "datafile",
        [typeof(StandaloneFileNodeViewModel)] = "standalone",
        [typeof(ArrangerNodeViewModel)] = "arranger",
        [typeof(PaletteNodeViewModel)] = "palette",
    }.ToFrozenDictionary();

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

        if (item.DataContext is not null && _nodeClassMap.TryGetValue(item.DataContext.GetType(), out var className))
        {
            item.Classes.Add(className);
        }
    }

    private static void RemoveAllClasses(TreeViewItem item)
    {
        foreach (var className in _nodeClassMap.Values)
        {
            item.Classes.Remove(className);
        }
    }
}
