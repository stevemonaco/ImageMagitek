using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Colors;
using Monaco.PathTree;
using Monaco.PathTree.Abstractions;

namespace ImageMagitek.Project
{
    public sealed class ProjectTree : PathTreeBase<ResourceNode, IProjectResource>
    {
        public ImageProject Project => Root?.Item as ImageProject;
        public string Name => Root.Item.Name;
        //public string FileLocation => (Root?.Metadata as ProjectMetadata).FileLocation;

        public ProjectTree(ProjectNode root, string fileLocation = default) :
            base(root)
        {
            if (root?.Item is not ImageProject)
                throw new ArgumentException($"{nameof(ProjectTree)} ctor called with invalid" +
                    $" root type '{root.GetType()}'");

            ExcludeRootFromPath = true;
        }

        /// <summary>
        /// Searches the entire tree to determine if the specified resource is contained within the tree
        /// </summary>
        /// <param name="resource">Resource to search</param>
        /// <returns></returns>
        public bool ContainsResource(IProjectResource resource)
        {
            return Root.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
                .Any(x => ReferenceEquals(x.Item, resource));
        }

        /// <summary>
        /// Compares the node's root ancestor with the project root to determine if the tree contains the node
        /// </summary>
        /// <param name="node">Node to search</param>
        public bool ContainsNode(ResourceNode node) =>
            ReferenceEquals(node.SelfAndAncestors<ResourceNode, IProjectResource>().Last(), Root);

        /// <summary>
        /// Tries to find the resource node that contains the specified resource
        /// </summary>
        /// <param name="resource">Resource to locate</param>
        /// <param name="resourceNode">Result of search</param>
        /// <returns>True if found, false if not found</returns>
        public bool TryFindResourceNode(IProjectResource resource, out ResourceNode resourceNode)
        {
            resourceNode = Root.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
                .FirstOrDefault(x => ReferenceEquals(x.Item, resource));

            return resourceNode is object;
        }

        /// <summary>
        /// Gets the resource node that contains the specified resource
        /// </summary>
        /// <param name="resource">Resource to locate</param>
        public ResourceNode GetResourceNode(IProjectResource resource) =>
            Root.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
                .First(x => ReferenceEquals(x.Item, resource));

        /// <summary>
        /// Gets the resource node that contains the specified resource
        /// </summary>
        /// <param name="resource">Resource to locate</param>
        public T GetResourceNode<T>(IProjectResource resource) where T : ResourceNode =>
            (T) Root.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
                .First(x => ReferenceEquals(x.Item, resource) && x is T);

        /// <summary>
        /// Creates a new folder node under the specified parent
        /// </summary>
        /// <param name="parentNode">Parent to the new folder</param>
        /// <param name="name">New name of the folder which will be augmented if already existing</param>
        /// <returns>The newly created ResourceNode</returns>
        public MagitekResult<ResourceNode> CreateNewFolder(ResourceNode parentNode, string name)
        {
            if (!ContainsNode(parentNode))
                return new MagitekResult<ResourceNode>.Failed($"{parentNode.Item.Name} is not contained within project {Root.Item.Name}");

            if (parentNode.ContainsChildNode(name) || !parentNode.Item.CanContainChildResources)
                return new MagitekResult<ResourceNode>.Failed($"Could not create folder '{name}' under parent '{parentNode.Name}'");

            try
            {
                var childName = FindFirstNewChildResourceName(parentNode, name);
                var directoryName = LocateResourceDirectoryByParentNode(parentNode, childName);
                Directory.CreateDirectory(directoryName);

                var folder = new ResourceFolder(childName);
                var node = new ResourceFolderNode(childName, folder);
                parentNode.AttachChildNode(node);
                return new MagitekResult<ResourceNode>.Success(node);
            }
            catch (Exception ex)
            {
                return new MagitekResult<ResourceNode>.Failed($"Could not create folder '{name}' under parent '{parentNode.Name}'\n{ex.Message}");
            }
        }

        /// <summary>
        /// Adds the specified resource to the parent resource node
        /// </summary>
        /// <param name="parentNode">ResourceNode that is contained by the project</param>
        /// <param name="resource">New resource to add</param>
        /// <returns></returns>
        public MagitekResult<ResourceNode> AddResource(ResourceNode parentNode, IProjectResource resource)
        {
            if (!ContainsNode(parentNode))
                return new MagitekResult<ResourceNode>.Failed($"{parentNode.Item.Name} is not contained within project {Root.Item.Name}");

            if (parentNode.ContainsChildNode(resource.Name))
            {
                return new MagitekResult<ResourceNode>.Failed($"'{parentNode.Name}' already contains a child named '{resource.Name}'");
            }
            else if (parentNode.Item.CanContainChildResources == false)
            {
                return new MagitekResult<ResourceNode>.Failed($"'{parentNode.Name}' cannot contain children");
            }
            else
            {
                ResourceNode childNode = resource switch
                {
                    DataFile df => new DataFileNode(df.Name, df),
                    ScatteredArranger arranger => new ArrangerNode(arranger.Name, arranger),
                    Palette pal => new PaletteNode(pal.Name, pal),
                    ResourceFolder folder => new ResourceFolderNode(folder.Name, folder),
                    _ => null
                };

                if (childNode is null)
                    return new MagitekResult<ResourceNode>.Failed($"Cannot add a resource of type '{resource.GetType()}'");

                parentNode.AttachChildNode(childNode);
                var childKey = CreatePathKey(childNode);
                childNode.DiskLocation = LocateResourceFileNameByNode(childNode);

                return new MagitekResult<ResourceNode>.Success(childNode);
            }
        }

        public MagitekResult CanMoveNode(ResourceNode node, ResourceNode parentNode)
        {
            if (node is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{nameof(node)}' was null");

            if (parentNode is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{nameof(parentNode)}' was null");

            if (node.Parent is null)
                return new MagitekResult.Failed($"{node.Name} has no parent");

            if (ReferenceEquals(node, parentNode))
                return new MagitekResult.Failed($"Cannot move {node.Name} onto itself");


            var nodeKey = CreatePathKey(node);
            var parentKey = CreatePathKey(parentNode);

            if (CreatePathKey(node.Parent) == parentKey)
                return new MagitekResult.Failed($"Cannot move {node.Name} onto itself");

            if (parentNode.ContainsChildNode(node.Name))
                return new MagitekResult.Failed($"{parentNode.Name} already contains {node.Name}");

            if (!parentNode.Item.CanContainChildResources)
                return new MagitekResult.Failed($"{parentNode.Name} cannot contain child resources");

            if (node.Item is ResourceFolder && parentNode.Item is ResourceFolder)
            {
                var keys = parentNode.Ancestors<ResourceNode, IProjectResource>().Select(x => CreatePathKey(x));
                if (keys.Any(x => x == nodeKey))
                    return new MagitekResult.Failed($"{parentNode.Name} cannot be moved underneath its child node");
            }

            if (!ContainsNode(node))
                return new MagitekResult.Failed($"{nodeKey} is not contained within project {Root.Item.Name}");

            if (!ContainsNode(parentNode))
                return new MagitekResult.Failed($"{parentKey} is not contained within project {Root.Item.Name}");

            return MagitekResult.SuccessResult;
        }

        public MagitekResult MoveNode(ResourceNode node, ResourceNode parentNode)
        {
            if (node is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{node}' was null");

            if (parentNode is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{parentNode}' was null");

            var canMoveResult = CanMoveNode(node, parentNode);

            if (canMoveResult.HasSucceeded)
            {
                try
                {
                    var oldFileName = node.DiskLocation;
                    var newFileName = LocateResourceFileNameByParentNode(parentNode, node.Name);
                    File.Move(oldFileName, newFileName, true);

                    node.Parent.DetachChildNode(node.Name);
                    parentNode.AttachChildNode(node);
                    node.DiskLocation = newFileName;
                    return MagitekResult.SuccessResult;
                }
                catch (Exception ex)
                {
                    return new MagitekResult.Failed($"Failed to move node: {ex.Message}");
                }
            }
            else
                return canMoveResult;
        }

        public MagitekResult RenameResource(ResourceNode resourceNode, string newName)
        {
            if (resourceNode.Parent.ContainsChildNode(newName))
                return new MagitekResult.Failed($"Parent node '{CreatePathKey(resourceNode.Parent)}' already contains a node named '{newName}'");

            if (resourceNode is ResourceFolderNode)
            {
                var oldDirectoryPath = resourceNode.DiskLocation;
                var newDirectoryPath = LocateResourceDirectoryByParentNode(resourceNode.Parent, newName);
                Directory.Move(oldDirectoryPath, newDirectoryPath);
                resourceNode.DiskLocation = newDirectoryPath;
                resourceNode.Rename(newName);
            }
            else
            {
                var oldFileName = resourceNode.DiskLocation;
                var newFileName = LocateResourceFileNameByParentNode(resourceNode.Parent, newName);
                File.Move(oldFileName, newFileName);
                resourceNode.DiskLocation = newFileName;
                resourceNode.Rename(newName);
            }

            return MagitekResult.SuccessResult;
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

            var rootRemovalChange = new ResourceChange(removeNode, CreatePathKey(removeNode), true, false, false);

            var removedDict = removeNode.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
                .Select(x => new ResourceChange(x, CreatePathKey(x), true, false, false))
                .ToDictionary(key => key.Resource, val => val);

            foreach (var node in removedDict.Values)
                yield return node;

            // Palettes with removed DataFiles must be checked early, so that Arrangers are effected in the main loop by removed Palettes
            var removedPaletteNodes = this.EnumerateDepthFirst()
                .Where(x => x.Item is Palette)
                .Where(x => removedDict.ContainsKey((x.Item as Palette).DataFile));

            foreach (var paletteNode in removedPaletteNodes)
            {
                var paletteChange = new ResourceChange(paletteNode, CreatePathKey(paletteNode), true, false, false);
                removedDict[paletteNode.Item] = paletteChange;
                yield return paletteChange;
            }

            foreach (var node in this.EnumerateDepthFirst().Where(x => !removedDict.ContainsKey(x.Item)))
            {
                var removed = false;
                var lostElements = false;
                var lostPalette = false;
                var resource = node.Item;

                foreach (var linkedResource in resource.LinkedResources)
                {
                    if (removedDict.ContainsKey(linkedResource))
                    {
                        if (linkedResource is Palette && resource is Arranger)
                            lostPalette = true;

                        if (linkedResource is DataFile && resource is Arranger arranger)
                        {
                            lostElements = true;
                            if (arranger.EnumerateElements().OfType<ArrangerElement>().All(x => removedDict.ContainsKey(linkedResource) || x.DataFile is null))
                                removed = true;
                        }
                    }
                }

                if (removed || lostPalette || lostElements)
                {
                    var change = new ResourceChange(node, CreatePathKey(node), removed, lostPalette, lostElements);
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
                resourceParent.RemoveChildNode(item.Resource.Name);
            }
        }

        private string FindFirstNewChildResourceName(ResourceNode node, string baseName)
        {
            if (!node.ContainsChildNode(baseName))
                return baseName;
            else
                return new string[] { baseName }
                .Concat(Enumerable.Range(1, 999).Select(x => $"{baseName} ({x})"))
                .FirstOrDefault(x => !node.ContainsChildNode(x));
        }

        private string LocateResourceFileNameByNode(ResourceNode node)
        {
            var baseDirectory = (Root as ProjectNode).BaseDirectory;
            var pathKey = CreatePathKey(node, Path.DirectorySeparatorChar.ToString()).TrimStart(Path.DirectorySeparatorChar);

            return Path.Combine(baseDirectory, $"{pathKey}.xml");
        }

        private string LocateResourceFileNameByParentNode(ResourceNode parentNode, string childName)
        {
            var baseDirectory = (Root as ProjectNode).BaseDirectory;
            var pathKey = CreatePathKey(parentNode, Path.DirectorySeparatorChar.ToString()).TrimStart(Path.DirectorySeparatorChar);

            return Path.Combine(baseDirectory, pathKey, $"{childName}.xml");
        }

        private string LocateResourceDirectoryByNode(ResourceNode node)
        {
            var baseDirectory = (Root as ProjectNode).BaseDirectory;
            var pathKey = CreatePathKey(node, Path.DirectorySeparatorChar.ToString()).TrimStart(Path.DirectorySeparatorChar);

            return Path.Combine(baseDirectory, pathKey);
        }

        private string LocateResourceDirectoryByParentNode(ResourceNode parentNode, string childName)
        {
            var baseDirectory = (Root as ProjectNode).BaseDirectory;
            var pathKey = CreatePathKey(parentNode, Path.DirectorySeparatorChar.ToString()).TrimStart(Path.DirectorySeparatorChar);

            return Path.Combine(baseDirectory, pathKey, childName);
        }
    }
}
