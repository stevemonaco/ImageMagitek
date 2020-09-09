using System;
using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Colors;
using Monaco.PathTree;

namespace ImageMagitek.Project
{
    public class ProjectTree
    {
        public IPathTree<IProjectResource> Tree { get; }
        public ImageProject Project => Tree?.Root?.Value as ImageProject;
        public string Name => Tree.Root.Value.Name;
        public string FileLocation { get; set; }

        public ProjectTree(IPathTree<IProjectResource> projectTree)
        {
            if (projectTree?.Root?.Value is ImageProject)
                Tree = projectTree;
            else
                throw new ArgumentException($"{nameof(ProjectTree)} ctor called with invalid" +
                    $"'{nameof(projectTree)}' with root type '{projectTree?.Root?.Value?.GetType()}'");
        }

        public ProjectTree(IPathTree<IProjectResource> projectTree, string fileLocation)
        {
            if (projectTree?.Root?.Value is ImageProject)
                Tree = projectTree;
            else
                throw new ArgumentException($"{nameof(ProjectTree)} ctor called with invalid" +
                    $"'{nameof(projectTree)}' with root type '{projectTree?.Root?.Value?.GetType()}'");

            FileLocation = fileLocation;
        }

        /// <summary>
        /// Searches the entire tree to determine if the specified resource is contained within the tree
        /// </summary>
        /// <param name="resource">Resource to search</param>
        /// <returns></returns>
        public bool ContainsResource(IProjectResource resource)
        {
            var nodes = Tree.Root.SelfAndDescendantsDepthFirst();
            return nodes.Any(x => ReferenceEquals(x.Value, resource));
        }

        /// <summary>
        /// Compares the node's root ancestor with the project root to determine if the tree contains the node
        /// </summary>
        /// <param name="node">Node to search</param>
        /// <returns></returns>
        public bool ContainsNode(ResourceNode node) =>
            ReferenceEquals(node.SelfAndAncestors().Last(), Tree.Root);

        public MagitekResult<ResourceNode> CreateNewFolder(ResourceNode parentNode, string name, bool useExactName)
        {
            if (!parentNode.ContainsChild(name) || !useExactName && parentNode.Value.CanContainChildResources)
            {
                var childName = FindFirstNewChildResourceName(parentNode, name);
                var folder = new ResourceFolder(childName);
                var folderNode = new FolderNode(folder.Name, folder);
                parentNode.AttachChild(folderNode);
                return new MagitekResult<ResourceNode>.Success(folderNode);
            }
            else // if (parentNode.ContainsChild(name) && useExactName)
                return new MagitekResult<ResourceNode>.Failed($"Could not create folder '{name}' under parent '{parentNode.Name}'");
        }

        public MagitekResult<ResourceNode> AddResource(ResourceNode parentNode, IProjectResource resource)
        {
            if (!ContainsNode(parentNode))
                return new MagitekResult<ResourceNode>.Failed($"{parentNode.Value} is not contained within project {Tree.Root.Value.Name}");

            if (parentNode.ContainsChild(resource.Name))
            {
                return new MagitekResult<ResourceNode>.Failed($"'{parentNode.Name}' already contains a child named '{resource.Name}'");
            }
            else if (parentNode.Value.CanContainChildResources == false)
            {
                return new MagitekResult<ResourceNode>.Failed($"'{parentNode.Name}' cannot contain children");
            }
            else
            {
                if (resource is DataFile || resource is ScatteredArranger || resource is Palette || resource is ResourceFolder)
                {
                    parentNode.AddChild(resource.Name, resource);
                    parentNode.TryGetChild(resource.Name, out var childNode);
                    return new MagitekResult<ResourceNode>.Success(childNode);
                }
                else
                {
                    return new MagitekResult<ResourceNode>.Failed($"Cannot add a resource of type '{resource.GetType()}'"); ;
                }
            }
        }

        public MagitekResult CanMoveNode(ResourceNode node, ResourceNode parentNode)
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

            if (node.Resource is ResourceFolder && parentNode.Resource is ResourceFolder)
            {
                if (parentNode.Ancestors().Any(x => x.PathKey == node.PathKey))
                    return new MagitekResult.Failed($"{parentNode.Name} cannot be moved underneath its child node");
            }

            if (!ContainsNode(node))
                return new MagitekResult.Failed($"{node.PathKey} is not contained within project {Tree.Root.Value.Name}");

            if (!ContainsNode(parentNode))
                return new MagitekResult.Failed($"{parentNode.PathKey} is not contained within project {Tree.Root.Value.Name}");

            return MagitekResult.SuccessResult;
        }

        public MagitekResult MoveNode(ResourceNode node, ResourceNode parentNode)
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

        /// <summary>
        /// Returns a preview changed resources if the specified node is removed
        /// </summary>
        /// <param name="removeNode"></param>
        /// <returns></returns>
        public IEnumerable<ResourceChange> GetSecondaryResourceRemovalChanges(ResourceNode removeNode)
        {
            if (removeNode is null)
                throw new ArgumentNullException($"{nameof(GetSecondaryResourceRemovalChanges)} parameter '{removeNode}' was null");

            if (!ContainsNode(removeNode))
                throw new ArgumentException($"{nameof(GetSecondaryResourceRemovalChanges)} value '{removeNode.Name}' was not found in the project tree");

            var rootRemovalChange = new ResourceChange(removeNode, true, false, false);

            var removedDict = removeNode.SelfAndDescendantsDepthFirst()
                .Cast<ResourceNode>()
                .Select(x => new ResourceChange(x, true, false, false))
                .ToDictionary(key => key.Resource, val => val);

            foreach (var node in removedDict.Values)
                yield return node;

            // Palettes with removed DataFiles must be checked early, so that Arrangers are effected in the main loop by removed Palettes
            var removedPaletteNodes = Tree.EnumerateDepthFirst()
                .Cast<ResourceNode>()
                .Where(x => x.Value is Palette)
                .Where(x => removedDict.ContainsKey((x.Value as Palette).DataFile));

            foreach (var paletteNode in removedPaletteNodes)
            {
                var paletteChange = new ResourceChange(paletteNode, true, false, false);
                removedDict[paletteNode.Value] = paletteChange;
                yield return paletteChange;
            }

            foreach (var node in Tree.EnumerateDepthFirst().Cast<ResourceNode>().Where(x => !removedDict.ContainsKey(x.Value)))
            {
                var removed = false;
                var lostElements = false;
                var lostPalette = false;
                var resource = node.Value;

                foreach (var linkedResource in resource.LinkedResources)
                {
                    if (removedDict.ContainsKey(linkedResource))
                    {
                        if (linkedResource is Palette && resource is Arranger)
                            lostPalette = true;

                        if (linkedResource is DataFile && resource is Arranger arranger)
                        {
                            lostElements = true;
                            if (arranger.EnumerateElements().All(x => removedDict.ContainsKey(linkedResource) || x.DataFile is null))
                                removed = true;
                        }
                    }
                }

                if (removed || lostPalette || lostElements)
                {
                    var change = new ResourceChange(node, removed, lostPalette, lostElements);
                    yield return change;
                }
            }
        }

        public void ApplyRemovalChanges(IList<ResourceChange> changes)
        {
            foreach (var item in changes.Where(x => x.IsChanged))
            {
                foreach (var removeItem in changes.Where(x => x.Removed))
                {
                    item.Resource.UnlinkResource(removeItem.Resource);
                }
            }

            foreach (var item in changes.Where(x => x.Removed))
            {
                var resourceParent = item.ResourceNode.Parent;
                resourceParent.RemoveChild(item.Resource.Name);
            }
        }

        private string FindFirstNewChildResourceName(ResourceNode node, string baseName)
        {
            if (!node.ContainsChild(baseName))
                return baseName;
            else
                return new string[] { baseName }
                .Concat(Enumerable.Range(1, 999).Select(x => $"{baseName} ({x})"))
                .FirstOrDefault(x => !node.ContainsChild(x));
        }
    }
}
