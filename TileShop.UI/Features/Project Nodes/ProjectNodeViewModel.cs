using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ImageMagitek.Project;

namespace TileShop.UI.ViewModels;

/// <summary>
/// Root of a project's view model tree, kept in sync with <see cref="ProjectTree.Changed"/>
/// </summary>
public class ProjectNodeViewModel : ResourceNodeViewModel
{
    private readonly Dictionary<ResourceNode, ResourceNodeViewModel> _viewModels = new();

    public override int SortPriority => 0;

    [SetsRequiredMembers]
    public ProjectNodeViewModel(ProjectTree tree)
    {
        Node = tree.Root;
        Name = tree.Root.Name;
        IsExpanded = true;

        AddChildren();
        Register(this);
        tree.Changed += OnTreeChanged;
    }

    public override ResourceNodeViewModel? Find(ResourceNode node) => _viewModels.GetValueOrDefault(node);

    private void OnTreeChanged(object? sender, ProjectTreeChange change)
    {
        switch (change.Kind)
        {
            case ProjectTreeChangeKind.Added when Find(change.Parent!) is { } parentVm:
                var addedVm = Create(change.Node, parentVm);
                parentVm.InsertChildSorted(addedVm);
                Register(addedVm);
                break;

            case ProjectTreeChangeKind.Removed when Find(change.Node) is { } removedVm:
                removedVm.ParentModel?.Children.Remove(removedVm);
                Unregister(removedVm);
                break;

            case ProjectTreeChangeKind.Moved when Find(change.Node) is { } movedVm && Find(change.Parent!) is { } newParentVm:
                movedVm.ParentModel?.Children.Remove(movedVm);
                movedVm.ParentModel = newParentVm;
                newParentVm.InsertChildSorted(movedVm);
                break;

            case ProjectTreeChangeKind.Renamed when Find(change.Node) is { } renamedVm:
                renamedVm.Name = change.Node.Name;
                renamedVm.ParentModel?.ResortChild(renamedVm);
                break;
        }
    }

    private void Register(ResourceNodeViewModel vm)
    {
        foreach (var child in vm.SelfAndDescendants())
            _viewModels[child.Node] = child;
    }

    private void Unregister(ResourceNodeViewModel vm)
    {
        foreach (var child in vm.SelfAndDescendants())
            _viewModels.Remove(child.Node);
    }
}
