using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

public abstract partial class ResourceNodeViewModel : ObservableObject
{
    public required ResourceNode Node { get; set; }
    public ResourceNodeViewModel? ParentModel { get; set; }
    public abstract int SortPriority { get; }

    [ObservableProperty] private ObservableCollection<ResourceNodeViewModel> _children = new();
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _name = "";

    public virtual ResourceNodeViewModel? Find(ResourceNode node) => ReferenceEquals(Node, node) ? this : null;

    /// <summary>
    /// Creates the view model for <paramref name="node"/> and its descendants, with children in sorted order
    /// </summary>
    public static ResourceNodeViewModel Create(ResourceNode node, ResourceNodeViewModel parent)
    {
        ResourceNodeViewModel vm = node switch
        {
            ResourceFolderNode => new FolderNodeViewModel(node, parent),
            PaletteNode => new PaletteNodeViewModel(node, parent),
            DataFileNode => new DataFileNodeViewModel(node, parent),
            ArrangerNode => new ArrangerNodeViewModel(node, parent),
            _ => throw new NotSupportedException($"{nameof(Create)}: a node '{node.Name}' of type '{node.GetType()}' is not supported")
        };

        vm.AddChildren();
        return vm;
    }

    protected void AddChildren()
    {
        foreach (var child in Node.ChildNodes.Select(x => Create(x, this)).Order(ResourceNodeComparer.Instance))
            Children.Add(child);
    }

    internal void InsertChildSorted(ResourceNodeViewModel child) =>
        Children.Insert(Children.Count(x => ResourceNodeComparer.Instance.Compare(x, child) <= 0), child);

    /// <summary>
    /// Moves <paramref name="child"/> to its sorted position, keeping its container and selection
    /// </summary>
    internal void ResortChild(ResourceNodeViewModel child)
    {
        var oldIndex = Children.IndexOf(child);
        var newIndex = Children.Where((x, i) => i != oldIndex && ResourceNodeComparer.Instance.Compare(x, child) <= 0).Count();
        if (newIndex != oldIndex)
            Children.Move(oldIndex, newIndex);
    }
}
