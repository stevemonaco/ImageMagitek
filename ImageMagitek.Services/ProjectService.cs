using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using Monaco.PathTree;

namespace ImageMagitek.Services
{
    /// <summary>
    /// Service class providing project management features and abstracts file handling
    /// </summary>
    public class ProjectService : IProjectService
    {
        public ISet<ProjectTree> Projects { get; } = new HashSet<ProjectTree>();
        private readonly IProjectSerializerFactory _serializerFactory;
        private readonly IColorFactory _colorFactory;

        public ProjectService(IProjectSerializerFactory serializerFactory, IColorFactory colorFactory)
        {
            _serializerFactory = serializerFactory;
            _colorFactory = colorFactory;
        }

        /// <summary>
        /// Creates a new project
        /// </summary>
        /// <param name="projectFileName">File name for the specified project</param>
        /// <returns></returns>
        public virtual MagitekResult<ProjectTree> NewProject(string projectFileName)
        {
            if (Projects.Any(x => string.Equals(x.Name, projectFileName, StringComparison.OrdinalIgnoreCase)))
                return new MagitekResult<ProjectTree>.Failed($"{projectFileName} already exists in the solution");

            var projectName = Path.GetFileNameWithoutExtension(projectFileName);
            var project = new ImageProject(projectName);
            var root = new ProjectNode(project.Name, project)
            {
                DiskLocation = Path.GetFullPath(projectFileName)
            };
            var tree = new ProjectTree(root);

            Projects.Add(tree);
            return new MagitekResult<ProjectTree>.Success(tree);
        }

        /// <summary>
        /// Opens the specified project and makes it active in the service
        /// </summary>
        /// <param name="projectFileName">Project to be opened</param>
        /// <returns>The opened project</returns>
        public virtual MagitekResults<ProjectTree> OpenProjectFile(string projectFileName)
        {
            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(OpenProjectFile)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            if (!File.Exists(projectFileName))
                return new MagitekResults<ProjectTree>.Failed($"File '{projectFileName}' does not exist");

            try
            {
                var reader = _serializerFactory.CreateReader();
                var result = reader.ReadProject(projectFileName);

                return result.Match(
                    success =>
                    {
                        Projects.Add(success.Result);
                        return result;
                    },
                    fail => result
                );
            }
            catch (Exception ex)
            {
                return new MagitekResults<ProjectTree>.Failed($"Failed to open project '{projectFileName}' due to a {ex.GetType()}: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the project
        /// </summary>
        /// <param name="projectTree">Project to be saved</param>
        /// <returns></returns>
        public virtual MagitekResult SaveProject(ProjectTree projectTree)
        {
            if (projectTree is null)
                throw new InvalidOperationException($"{nameof(SaveProject)} parameter '{nameof(projectTree)}' was null");

            string projectFileLocation = projectTree.Root.DiskLocation;

            if (string.IsNullOrWhiteSpace(projectFileLocation))
                throw new InvalidOperationException($"{nameof(SaveProject)} cannot have a null or empty value for the project's file location");

            try
            {
                var writer = _serializerFactory.CreateWriter(projectTree);
                return writer.WriteProject(projectFileLocation);
            }
            catch (Exception ex)
            {
                return new MagitekResult.Failed($"Failed to save project: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the project to another location on disk, leaving the old location intact
        /// </summary>
        /// <param name="projectTree">Project to be saved</param>
        /// <param name="projectFileName">New location</param>
        /// <returns></returns>
        public virtual MagitekResult SaveProjectAs(ProjectTree projectTree, string projectFileName)
        {
            if (projectTree is null)
                throw new InvalidOperationException($"{nameof(SaveProjectAs)} parameter '{nameof(projectTree)}' was null");

            if (string.IsNullOrWhiteSpace(projectFileName))
                throw new ArgumentException($"{nameof(SaveProjectAs)} cannot have a null or empty value for '{nameof(projectFileName)}'");

            try
            {
                var serializer = _serializerFactory.CreateWriter(projectTree);
                var result = serializer.WriteProject(projectFileName);
                if (result.Value is MagitekResult.Success)
                {
                    projectTree.Root.DiskLocation = Path.GetFullPath(projectFileName);
                }

                return result;
            }
            catch (Exception ex)
            {
                return new MagitekResult.Failed($"Failed to save project: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes the specified project that is active in the service and frees associated resources
        /// </summary>
        /// <param name="projectTree">Project to be closed</param>
        public virtual void CloseProject(ProjectTree projectTree)
        {
            if (projectTree is null)
                throw new InvalidOperationException($"{nameof(CloseProject)} parameter '{nameof(projectTree)}' was null");

            if (Projects.Contains(projectTree))
            {
                foreach (var file in projectTree.EnumerateBreadthFirst().Select(x => x.Item).OfType<DataFile>())
                    file.Close();

                Projects.Remove(projectTree);
            }
        }

        /// <summary>
        /// Closes all projects and frees their associated resources
        /// </summary>
        public virtual void CloseProjects()
        {
            var files = Projects.SelectMany(tree => tree.EnumerateDepthFirst().Select(x => x.Item).OfType<DataFile>());

            foreach (var file in files)
                file.Close();

            Projects.Clear();
        }

        /// <summary>
        /// Adds the specified resource to the parent resource node
        /// </summary>
        /// <param name="parentNode">ResourceNode that is contained by the project</param>
        /// <param name="resource">New resource to add</param>
        /// <returns></returns>
        public virtual MagitekResult<ResourceNode> AddResource(ResourceNode parentNode, IProjectResource resource)
        {
            var tree = Projects.FirstOrDefault(x => x.ContainsNode(parentNode));

            if (tree is null)
                return new MagitekResult<ResourceNode>.Failed($"{parentNode.Item.Name} is not contained within any loaded project");

            if (parentNode.ContainsChildNode(resource.Name))
            {
                return new MagitekResult<ResourceNode>.Failed($"'{parentNode.Name}' already contains a child named '{resource.Name}'");
            }
            else if (parentNode.Item.CanContainChildResources == false)
            {
                return new MagitekResult<ResourceNode>.Failed($"'{parentNode.Name}' cannot contain children");
            }

            try
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

                var contents = _serializerFactory.CreateWriter(tree).SerializeResource(childNode);
                var location = LocateResourceDiskLocationByParent(tree, parentNode, childNode);
                File.WriteAllText(location, contents);

                parentNode.AttachChildNode(childNode);
                childNode.DiskLocation = location;
                UpdateNodeModel(tree, childNode);

                return new MagitekResult<ResourceNode>.Success(childNode);
            }
            catch (Exception ex)
            {
                return new MagitekResult<ResourceNode>.Failed(ex.Message);
            }
        }

        /// <summary>
        /// Creates a new folder node under the specified parent
        /// </summary>
        /// <param name="parentNode">Parent to the new folder</param>
        /// <param name="name">New name of the folder which will be augmented if already existing</param>
        /// <returns>The newly created ResourceNode</returns>
        public virtual MagitekResult<ResourceNode> CreateNewFolder(ResourceNode parentNode, string name)
        {
            var tree = Projects.FirstOrDefault(x => x.ContainsNode(parentNode));

            if (tree is null)
                return new MagitekResult<ResourceNode>.Failed($"{parentNode.Item.Name} is not contained within any loaded project");

            if (parentNode.ContainsChildNode(name) || !parentNode.Item.CanContainChildResources)
                return new MagitekResult<ResourceNode>.Failed($"Could not create folder '{name}' under parent '{parentNode.Name}'");

            try
            {
                var childName = FindFirstNewChildResourceName(parentNode, name);
                var folder = new ResourceFolder(childName);
                var node = new ResourceFolderNode(childName, folder);
                var directoryName = LocateResourceDiskLocationByParent(tree, parentNode, node);

                Directory.CreateDirectory(directoryName);
                parentNode.AttachChildNode(node);

                return new MagitekResult<ResourceNode>.Success(node);
            }
            catch (Exception ex)
            {
                return new MagitekResult<ResourceNode>.Failed($"Could not create folder '{name}' under parent '{parentNode.Name}'\n{ex.Message}");
            }

            string FindFirstNewChildResourceName(ResourceNode node, string baseName)
            {
                if (!node.ContainsChildNode(baseName))
                    return baseName;
                else
                    return new string[] { baseName }
                    .Concat(Enumerable.Range(1, 999).Select(x => $"{baseName} ({x})"))
                    .FirstOrDefault(x => !node.ContainsChildNode(x));
            }
        }

        public virtual MagitekResult SaveResource(ProjectTree projectTree, ResourceNode resourceNode, bool alwaysOverwrite)
        {
            if (projectTree is null)
                throw new InvalidOperationException($"{nameof(SaveResource)} parameter '{nameof(projectTree)}' was null");

            string projectFileLocation = projectTree.Root.DiskLocation;

            if (string.IsNullOrWhiteSpace(projectFileLocation))
                throw new InvalidOperationException($"{nameof(SaveResource)} cannot have a null or empty value for the project's file location");

            try
            {
                if (resourceNode is PaletteNode paletteNode)
                {
                    (paletteNode.Item as Palette).SavePalette();
                }

                var writer = _serializerFactory.CreateWriter(projectTree);
                return writer.WriteResource(resourceNode, alwaysOverwrite);
            }
            catch (Exception ex)
            {
                return new MagitekResult.Failed($"Failed to save project resource '{resourceNode.Name}': {ex.Message}");
            }
        }

        public virtual ProjectTree GetContainingProject(ResourceNode node)
        {
            return Projects.FirstOrDefault(x => x.ContainsNode(node)) ??
                throw new ArgumentException($"{nameof(GetContainingProject)} could not locate the node '{node.Name}'");
        }

        public virtual ProjectTree GetContainingProject(IProjectResource resource)
        {
            return Projects.FirstOrDefault(x => x.ContainsResource(resource)) ??
                throw new ArgumentException($"{nameof(GetContainingProject)} could not locate the resource '{resource.Name}'");
        }

        public virtual bool AreResourcesInSameProject(IProjectResource a, IProjectResource b)
        {
            var projectA = Projects.FirstOrDefault(x => x.ContainsResource(a));
            var projectB = Projects.FirstOrDefault(x => x.ContainsResource(b));

            return ReferenceEquals(projectA, projectB);
        }

        /// <summary>
        /// Renames a node to the specified name
        /// </summary>
        /// <param name="node"></param>
        /// <param name="newName"></param>
        /// <returns></returns>
        public virtual MagitekResult RenameResource(ResourceNode node, string newName)
        {
            var tree = Projects.FirstOrDefault(x => x.ContainsNode(node));

            if (node.Parent is not null && node.Parent.ContainsChildNode(newName))
                return new MagitekResult.Failed($"Parent node '{tree.CreatePathKey(node.Parent)}' already contains a node named '{newName}'");

            var oldLocation = node.DiskLocation;
            var newLocation = LocateResourceDiskLocationByParent(tree, node.Parent, node);

            if (node is ResourceFolderNode)
            {
                Directory.Move(oldLocation, newLocation);
                node.DiskLocation = newLocation;
                node.Rename(newName);
            }
            else
            {
                var oldName = node.Name;

                try
                {
                    node.Rename(newName);
                    node.Item.Name = newName;
                    var contents = _serializerFactory.CreateWriter(tree).SerializeResource(node);

                    File.WriteAllText(newLocation, contents);
                    File.Delete(oldLocation);
                    node.DiskLocation = newLocation;

                    UpdateNodeModel(tree, node);
                }
                catch (Exception ex)
                {
                    node.Rename(oldName);
                    node.Item.Name = oldName;
                    return new MagitekResult.Failed($"Could not rename '{node.Name}': {ex.Message}");
                }
            }

            return MagitekResult.SuccessResult;
        }

        /// <summary>
        /// Checks if the specified node can be assigned as a child to the specified parent node
        /// </summary>
        /// <param name="node"></param>
        /// <param name="parentNode"></param>
        /// <returns></returns>
        public virtual MagitekResult CanMoveNode(ResourceNode node, ResourceNode parentNode)
        {
            if (node is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{nameof(node)}' was null");

            if (parentNode is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{nameof(parentNode)}' was null");

            var tree = GetContainingProject(node);
            if (!tree.ContainsNode(parentNode))
                return new MagitekResult.Failed($"Nodes must be located within the same project");

            if (node.Parent is null)
                return new MagitekResult.Failed($"{node.Name} has no parent");

            if (ReferenceEquals(node, parentNode))
                return new MagitekResult.Failed($"Cannot move {node.Name} onto itself");

            var nodeKey = tree.CreatePathKey(node);
            var parentKey = tree.CreatePathKey(parentNode);

            if (tree.CreatePathKey(node.Parent) == parentKey)
                return new MagitekResult.Failed($"Cannot move {node.Name} onto itself");

            if (parentNode.ContainsChildNode(node.Name))
                return new MagitekResult.Failed($"{parentNode.Name} already contains {node.Name}");

            if (!parentNode.Item.CanContainChildResources)
                return new MagitekResult.Failed($"{parentNode.Name} cannot contain child resources");

            if (node.Item is ResourceFolder && parentNode.Item is ResourceFolder)
            {
                var keys = parentNode.Ancestors<ResourceNode, IProjectResource>().Select(x => tree.CreatePathKey(x));
                if (keys.Any(x => x == nodeKey))
                    return new MagitekResult.Failed($"{parentNode.Name} cannot be moved underneath its child node");
            }

            if (!tree.ContainsNode(node))
                return new MagitekResult.Failed($"{nodeKey} is not contained within project {tree.Root.Item.Name}");

            if (!tree.ContainsNode(parentNode))
                return new MagitekResult.Failed($"{parentKey} is not contained within project {tree.Root.Item.Name}");

            return MagitekResult.SuccessResult;
        }

        /// <summary>
        /// Moves the specified node to be a child of the specified parent, if possible
        /// </summary>
        /// <param name="node"></param>
        /// <param name="parentNode"></param>
        /// <returns></returns>
        public virtual MagitekResult MoveNode(ResourceNode node, ResourceNode parentNode)
        {
            if (node is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{node}' was null");

            if (parentNode is null)
                throw new ArgumentNullException($"{nameof(CanMoveNode)} parameter '{parentNode}' was null");

            var canMoveResult = CanMoveNode(node, parentNode);
            var tree = GetContainingProject(node);

            if (canMoveResult.HasSucceeded)
            {
                try
                {
                    var oldFileName = node.DiskLocation;
                    var newFileName = LocateResourceDiskLocationByParent(tree, parentNode, node);
                    
                    var contents = _serializerFactory.CreateWriter(tree).SerializeResource(node);
                    File.WriteAllText(newFileName, contents);
                    File.Delete(node.DiskLocation);
                    node.DiskLocation = newFileName;
                    
                    node.Parent.DetachChildNode(node.Name);
                    parentNode.AttachChildNode(node);
                    UpdateNodeModel(tree, node);
                    return SaveProject(tree);
                }
                catch (Exception ex)
                {
                    return new MagitekResult.Failed($"Failed to move node: {ex.Message}");
                }
            }
            else
                return canMoveResult;
        }

        /// <summary>
        /// Applies deletion and modification changes to the tree
        /// </summary>
        /// <param name="changes">Changes to be applied</param>
        /// <param name="defaultPalette">Default palette to fallback to when a resource loses a palette</param>
        /// <returns></returns>
        public virtual MagitekResult ApplyResourceDeletionChanges(IList<ResourceChange> changes, Palette defaultPalette)
        {
            var tree = GetContainingProject(changes.First().ResourceNode);
            var removedItems = changes.Where(x => x.Removed).ToList();

            foreach (var change in changes.Where(x => x.IsChanged))
            {
                foreach (var removeItem in removedItems)
                {
                    change.Resource.UnlinkResource(removeItem.Resource);
                }

                if (change.LostPalette && change.Resource is ScatteredArranger arranger)
                {
                    foreach (var (x, y) in arranger.EnumerateElementLocations())
                    {
                        var el = arranger.GetElement(x, y);
                        if (el is object && el?.Palette is null)
                            arranger.SetElement(el.Value.WithPalette(defaultPalette), x, y);
                    }
                }

                var contents = _serializerFactory.CreateWriter(tree).SerializeResource(change.ResourceNode);
                var location = change.ResourceNode.DiskLocation;

                File.WriteAllText(location, contents);
                UpdateNodeModel(tree, change.ResourceNode);
            }

            foreach (var item in removedItems.Where(x => x.Resource is not ResourceFolder))
            {
                var resourceParent = item.ResourceNode.Parent;
                resourceParent.RemoveChildNode(item.Resource.Name);
                File.Delete(item.ResourceNode.DiskLocation);
            }

            foreach (var item in removedItems.Where(x => x.Resource is ResourceFolder))
            {
                var resourceParent = item.ResourceNode.Parent;
                resourceParent.RemoveChildNode(item.Resource.Name);
                Directory.Delete(item.ResourceNode.DiskLocation);
            }

            return MagitekResult.SuccessResult;
        }

        /// <summary>
        /// Previews a list of changes/deletions that will happen if the specified node is deleted
        /// </summary>
        /// <param name="deleteNode">Node to preview deletion of</param>
        /// <returns></returns>
        public virtual IEnumerable<ResourceChange> PreviewResourceDeletionChanges(ResourceNode deleteNode)
        {
            if (deleteNode is null)
                throw new ArgumentNullException($"{nameof(PreviewResourceDeletionChanges)} parameter '{deleteNode}' was null");

            var tree = Projects.FirstOrDefault(x => x.ContainsNode(deleteNode));

            var rootRemovalChange = new ResourceChange(deleteNode, tree.CreatePathKey(deleteNode), true, false, false);

            var removedDict = deleteNode.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
                .Select(x => new ResourceChange(x, tree.CreatePathKey(x), true, false, false))
                .ToDictionary(key => key.Resource, val => val);

            foreach (var node in removedDict.Values)
                yield return node;

            // Palettes with removed DataFiles must be checked early, so that Arrangers are effected in the main loop by removed Palettes
            var removedPaletteNodes = tree.EnumerateDepthFirst()
                .Where(x => x.Item is Palette)
                .Where(x => removedDict.ContainsKey((x.Item as Palette).DataFile));

            foreach (var paletteNode in removedPaletteNodes)
            {
                var paletteChange = new ResourceChange(paletteNode, tree.CreatePathKey(paletteNode), true, false, false);
                removedDict[paletteNode.Item] = paletteChange;
                yield return paletteChange;
            }

            foreach (var node in tree.EnumerateDepthFirst().Where(x => !removedDict.ContainsKey(x.Item)))
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
                    var change = new ResourceChange(node, tree.CreatePathKey(node), removed, lostPalette, lostElements);
                    yield return change;
                }
            }
        }

        private static string LocateResourceDiskLocation(ProjectTree tree, ResourceNode node)
        {
            var baseDirectory = (tree.Root as ProjectNode).BaseDirectory;
            var pathKey = tree.CreatePathKey(node, Path.DirectorySeparatorChar.ToString()).TrimStart(Path.DirectorySeparatorChar);

            if (node.Item is ResourceFolder)
                return Path.Combine(baseDirectory, pathKey);
            else
                return Path.Combine(baseDirectory, $"{pathKey}.xml");
        }

        private static string LocateResourceDiskLocationByParent(ProjectTree tree, ResourceNode parentNode, ResourceNode childNode)
        {
            var baseDirectory = (tree.Root as ProjectNode).BaseDirectory;
            var pathKey = tree.CreatePathKey(parentNode, Path.DirectorySeparatorChar.ToString()).TrimStart(Path.DirectorySeparatorChar);

            if (childNode is not ResourceFolderNode)
                return Path.Combine(baseDirectory, pathKey, $"{childNode.Name}.xml");
            else
                return Path.Combine(baseDirectory, pathKey, childNode.Name);
        }

        private void UpdateNodeModel(ProjectTree tree, ResourceNode node)
        {
            if (node is ProjectNode projectNode)
            {
                projectNode.Model = (projectNode.Item as ImageProject).MapToModel();
            }
            else if (node is DataFileNode dfNode)
            {
                dfNode.Model = (dfNode.Item as DataFile).MapToModel();
            }
            else if (node is PaletteNode palNode)
            {
                var map = GetResourceMap(tree);
                palNode.Model = (palNode.Item as Palette).MapToModel(map, _colorFactory);
            }
            else if (node is ArrangerNode arrangerNode)
            {
                var map = GetResourceMap(tree);
                arrangerNode.Model = (arrangerNode.Item as ScatteredArranger).MapToModel(map);
            }
            else
                throw new NotSupportedException($"{nameof(UpdateNodeModel)} is not supported for node of type '{node.GetType()}'");

            Dictionary<IProjectResource, string> GetResourceMap(ProjectTree tree)
            {
                var resourceMap = new Dictionary<IProjectResource, string>();

                foreach (var resource in _serializerFactory.GlobalResources)
                    resourceMap.Add(resource, resource.Name);

                foreach (var node in tree.EnumerateDepthFirst().Where(x => x is not ResourceFolderNode))
                    resourceMap.Add(node.Item, tree.CreatePathKey(node));

                return resourceMap;
            }
        }
    }
}
