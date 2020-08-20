using System;
using System.Linq;
using ImageMagitek.Colors;
using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public class ProjectTree
    {
        public IPathTree<IProjectResource> Tree { get; }
        public ImageProject Project => Tree?.Root?.Value as ImageProject;

        public ProjectTree(IPathTree<IProjectResource> projectTree)
        {
            if (projectTree.Root.Value is ImageProject)
                Tree = projectTree;
            else
                throw new ArgumentException($"{nameof(ProjectTree)} ctor called with invalid" +
                    $"'{nameof(projectTree)}' with root type '{projectTree.Root.Value.GetType()}'");
        }

        /// <summary>
        /// Searches the entire tree to determine if the specified resource is contained within the tree
        /// </summary>
        /// <param name="resource">Resource to search</param>
        /// <returns></returns>
        public bool ContainsResource(IProjectResource resource) =>
            Tree.Root.SelfAndDescendantsBreadthFirst().Any(x => ReferenceEquals(x, resource));

        /// <summary>
        /// Compares the node's root ancestor with the project root to determine if the tree contains the node
        /// </summary>
        /// <param name="node">Node to search</param>
        /// <returns></returns>
        public bool ContainsNode(IPathTreeNode<IProjectResource> node) =>
            ReferenceEquals(node.Ancestors().Last(), Tree.Root);

        MagitekResult<ResourceFolder> CreateNewFolder(IPathTreeNode<IProjectResource> parentNode, string name, bool useExactName)
        {
            if (!parentNode.ContainsChild(name) || !useExactName && parentNode.Value.CanContainChildResources)
            {
                var childName = FindFirstNewChildResourceName(parentNode, name);
                var folder = new ResourceFolder(childName);
                parentNode.AddChild(childName, folder);
                return new MagitekResult<ResourceFolder>.Success(folder);
            }
            else // if (parentNode.ContainsChild(name) && useExactName)
                return new MagitekResult<ResourceFolder>.Failed($"Could not create folder '{name}' under parent '{parentNode.Name}'");
        }

        MagitekResult<IPathTreeNode<IProjectResource>> AddResource(IPathTreeNode<IProjectResource> parentNode, IProjectResource resource)
        {
            if (!ContainsNode(parentNode))
                return new MagitekResult<IPathTreeNode<IProjectResource>>.Failed($"{parentNode.Value} is not contained within project {Tree.Root.Value.Name}");

            if (parentNode.ContainsChild(resource.Name))
            {
                return new MagitekResult<IPathTreeNode<IProjectResource>>.Failed($"'{parentNode.Name}' already contains a child named '{resource.Name}'");
            }
            else if (parentNode.Value.CanContainChildResources == false)
            {
                return new MagitekResult<IPathTreeNode<IProjectResource>>.Failed($"'{parentNode.Name}' cannot contain children");
            }
            else
            {
                if (resource is DataFile || resource is ScatteredArranger || resource is Palette || resource is ResourceFolder)
                {
                    parentNode.AddChild(resource.Name, resource);
                    parentNode.TryGetChild(resource.Name, out var childNode);
                    return new MagitekResult<IPathTreeNode<IProjectResource>>.Success(childNode);
                }
                else
                {
                    return new MagitekResult<IPathTreeNode<IProjectResource>>.Failed($"Cannot add a resource of type '{resource.GetType()}'"); ;
                }
            }
        }

        MagitekResult CanMoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode)
        {
            if (node is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{node}' was null");

            if (parentNode is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{parentNode}' was null");

            if (ReferenceEquals(node, parentNode))
                return new MagitekResult.Failed($"Cannot move {node.Name} onto itself");

            if (node.Parent.PathKey == parentNode.PathKey)
                return new MagitekResult.Failed($"Cannot move {node.Name} onto itself");

            if (parentNode.ContainsChild(node.Name))
                return new MagitekResult.Failed($"{parentNode.Name} already contains {node.Name}");

            if (!parentNode.Value.CanContainChildResources)
                return new MagitekResult.Failed($"{parentNode.Name} cannot contain child resources");

            if (node is ResourceFolder && parentNode is ResourceFolder)
            {
                if (parentNode.Ancestors().Any(x => x.PathKey == node.PathKey))
                    return new MagitekResult.Failed($"{parentNode.Name} cannot contain child resources");
            }

            if (!ContainsNode(node))
                return new MagitekResult.Failed($"{node.Value} is not contained within project {Tree.Root.Value.Name}");

            if (!ContainsNode(parentNode))
                return new MagitekResult.Failed($"{parentNode.Value} is not contained within project {Tree.Root.Value.Name}");

            return MagitekResult.SuccessResult;
        }

        public MagitekResult MoveNode(IPathTreeNode<IProjectResource> node, IPathTreeNode<IProjectResource> parentNode)
        {
            if (node is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{node}' was null");

            if (parentNode is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{parentNode}' was null");

            return CanMoveNode(node, parentNode).Match<MagitekResult>(
                success =>
                {
                    node.Parent.DetachChild(node.Name);
                    parentNode.AttachChild(node);
                    return MagitekResult.SuccessResult;
                },
                failed => failed
                );
        }

        private string FindFirstNewChildResourceName(IPathTreeNode<IProjectResource> node, string baseName)
        {
            if (node.ContainsChild(baseName))
                return baseName;
            else
                return new string[] { baseName }
                .Concat(Enumerable.Range(1, 999).Select(x => $"{baseName} ({x})"))
                .FirstOrDefault(x => !node.ContainsChild(x));
        }
    }
}
