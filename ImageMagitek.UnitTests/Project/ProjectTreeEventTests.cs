using System.Collections.Generic;
using ImageMagitek.Project;
using Xunit;

namespace ImageMagitek.UnitTests.Project;

public sealed class ProjectTreeEventTests
{
    private readonly ProjectTree _tree;
    private readonly List<ProjectTreeChange> _changes = [];

    public ProjectTreeEventTests()
    {
        var project = new ImageProject("Project");
        _tree = new ProjectTree(new ProjectNode("", project.Name, project));
        _tree.Changed += (_, change) => _changes.Add(change);
    }

    private static ResourceFolderNode CreateFolder(string name) => new(name, new ResourceFolder(name));

    private static DataFileNode CreateDataFile(string name) => new(name, new MemoryDataSource(name, 16));

    [Fact]
    public void AttachChildNode_RaisesSingleAdded()
    {
        var folder = CreateFolder("Folder");

        _tree.Root.AttachChildNode(folder);

        var change = Assert.Single(_changes);
        Assert.Equal(ProjectTreeChangeKind.Added, change.Kind);
        Assert.Same(folder, change.Node);
        Assert.Same(_tree.Root, change.Parent);
    }

    [Fact]
    public void AttachChildNode_UnattachedParent_RaisesNothing()
    {
        var folder = CreateFolder("Folder");

        folder.AttachChildNode(CreateDataFile("data"));

        Assert.Empty(_changes);
    }

    [Fact]
    public void RemoveChildNode_RaisesRemovedAndClearsParent()
    {
        var folder = CreateFolder("Folder");
        _tree.Root.AttachChildNode(folder);
        _changes.Clear();

        _tree.Root.RemoveChildNode("Folder");

        var change = Assert.Single(_changes);
        Assert.Equal(ProjectTreeChangeKind.Removed, change.Kind);
        Assert.Same(folder, change.Node);
        Assert.Same(_tree.Root, change.Parent);
        Assert.Null(folder.Parent);
        Assert.False(_tree.ContainsNode(folder));
    }

    [Fact]
    public void DetachChildNode_RaisesRemoved()
    {
        var data = CreateDataFile("data");
        _tree.Root.AttachChildNode(data);
        _changes.Clear();

        _tree.Root.DetachChildNode("data");

        var change = Assert.Single(_changes);
        Assert.Equal(ProjectTreeChangeKind.Removed, change.Kind);
        Assert.Same(data, change.Node);
    }

    [Fact]
    public void Rename_RaisesSingleRenamedWithOldName()
    {
        var data = CreateDataFile("data");
        _tree.Root.AttachChildNode(data);
        _changes.Clear();

        data.Rename("renamed");

        var change = Assert.Single(_changes);
        Assert.Equal(ProjectTreeChangeKind.Renamed, change.Kind);
        Assert.Same(data, change.Node);
        Assert.Same(_tree.Root, change.Parent);
        Assert.Equal("data", change.OldName);
        Assert.Equal("renamed", data.Item.Name);
    }

    [Fact]
    public void MoveTo_RaisesSingleMovedWithOldParent()
    {
        var folder = CreateFolder("Folder");
        var data = CreateDataFile("data");
        _tree.Root.AttachChildNode(folder);
        _tree.Root.AttachChildNode(data);
        _changes.Clear();

        data.MoveTo(folder);

        var change = Assert.Single(_changes);
        Assert.Equal(ProjectTreeChangeKind.Moved, change.Kind);
        Assert.Same(data, change.Node);
        Assert.Same(folder, change.Parent);
        Assert.Same(_tree.Root, change.OldParent);
        Assert.Same(folder, data.Parent);
        Assert.False(_tree.Root.ContainsChildNode("data"));
    }

    [Fact]
    public void Index_FindsResourcesAfterAddRenameAndMove()
    {
        var folder = CreateFolder("Folder");
        var data = CreateDataFile("data");
        _tree.Root.AttachChildNode(folder);
        _tree.Root.AttachChildNode(data);

        data.Rename("renamed");
        data.MoveTo(folder);

        Assert.True(_tree.TryFindResourceNode(data.Item, out var found));
        Assert.Same(data, found);
        Assert.Same(folder, _tree.GetResourceNode<ResourceFolderNode>(folder.Item));
        Assert.True(_tree.ContainsResource(_tree.Root.Item));
    }

    [Fact]
    public void Index_AttachedSubtree_IndexesDescendants()
    {
        var folder = CreateFolder("Folder");
        var data = CreateDataFile("data");
        folder.AttachChildNode(data);

        _tree.Root.AttachChildNode(folder);

        Assert.True(_tree.ContainsResource(data.Item));
    }

    [Fact]
    public void Index_RemovedFolder_DropsDescendants()
    {
        var folder = CreateFolder("Folder");
        var data = CreateDataFile("data");
        _tree.Root.AttachChildNode(folder);
        folder.AttachChildNode(data);

        _tree.Root.RemoveChildNode("Folder");

        Assert.False(_tree.ContainsResource(folder.Item));
        Assert.False(_tree.ContainsResource(data.Item));
    }

    [Fact]
    public void DataWritten_AttachedSource_RaisesResourceChanged()
    {
        var data = CreateDataFile("data");
        _tree.Root.AttachChildNode(data);
        var changed = new List<IProjectResource>();
        _tree.ResourceChanged += (_, resource) => changed.Add(resource);

        ((DataSource)data.Item).NotifyDataWritten();

        Assert.Same(data.Item, Assert.Single(changed));
    }

    [Fact]
    public void DataWritten_RemovedSource_RaisesNothing()
    {
        var data = CreateDataFile("data");
        _tree.Root.AttachChildNode(data);
        _tree.Root.RemoveChildNode("data");
        var changed = new List<IProjectResource>();
        _tree.ResourceChanged += (_, resource) => changed.Add(resource);

        ((DataSource)data.Item).NotifyDataWritten();

        Assert.Empty(changed);
    }
}
