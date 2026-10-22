using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Metatiles;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
using ImageMagitek.Tables;
using ImageMagitek.Utility;
using Monaco.PathTree;

namespace ImageMagitek.Services;

/// <summary>
/// Service class providing project management features including updating stale references and file persistence
/// </summary>
public class ProjectService : IProjectService
{
    private readonly ISet<ProjectTree> _projects = new HashSet<ProjectTree>();
    private readonly IProjectSerializerFactory _serializerFactory;

    // One lock for all trees: every transaction in a directory shares its journal and staging paths
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public event EventHandler<ProjectTree>? ProjectOpened;
    public event EventHandler<ProjectTree>? ProjectClosed;
    public event EventHandler<ProjectTreeChange>? TreeChanged;
    public event EventHandler<IProjectResource>? ResourceChanged;

    public ProjectService(IProjectSerializerFactory serializerFactory)
    {
        _serializerFactory = serializerFactory;
    }

    private void AddProject(ProjectTree tree)
    {
        if (!_projects.Add(tree))
            return;

        tree.Changed += OnTreeChanged;
        tree.ResourceChanged += OnResourceChanged;
    }

    private void RemoveProject(ProjectTree tree)
    {
        if (!_projects.Remove(tree))
            return;

        tree.Changed -= OnTreeChanged;
        tree.ResourceChanged -= OnResourceChanged;
    }

    private static string StandaloneFileReason(ProjectTree tree) => $"'{tree.Name}' is a standalone file, not a project";

    private void OnTreeChanged(object? sender, ProjectTreeChange change) => TreeChanged?.Invoke(sender, change);
    private void OnResourceChanged(object? sender, IProjectResource resource) => ResourceChanged?.Invoke(sender, resource);

    // No ConfigureAwait(false): tree events raised after the awaited write must stay on the caller's context
    private async Task<T> WithWriteLockAsync<T>(Func<Task<T>> operation)
    {
        await _writeLock.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private bool IsOpenAt(string fullPath) =>
        _projects.Any(x => string.Equals(x.Root.DiskLocation, fullPath, StringComparison.OrdinalIgnoreCase));

    private static string? FindXmlConflict(string directory)
    {
        try
        {
            return XmlProjectReader.FindLoadableXml(directory) is { } file
                ? $"'{directory}' already holds '{Path.GetRelativePath(directory, file)}', which would be loaded as part of the project"
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"'{directory}' could not be checked for XML files that would be loaded as part of the project: {ex.Message}";
        }
    }

    /// <summary>
    /// Creates a new empty project and writes its project file
    /// </summary>
    /// <param name="projectFileName">File name for the specified project</param>
    public virtual Task<MagitekResult<ProjectTree>> CreateNewProjectAsync(string projectFileName) =>
        WithWriteLockAsync(() => CreateNewProjectCoreAsync(projectFileName));

    private async Task<MagitekResult<ProjectTree>> CreateNewProjectCoreAsync(string projectFileName)
    {
        var fullPath = Path.GetFullPath(projectFileName);
        var directory = Path.GetDirectoryName(fullPath)!;
        var projectName = Path.GetFileNameWithoutExtension(fullPath);

        var nameResult = ResourceName.Validate(projectName, false);
        if (nameResult.HasFailed)
            return new MagitekResult<ProjectTree>.Failed(nameResult.AsError.Reason);

        if (File.Exists(fullPath))
            return new MagitekResult<ProjectTree>.Failed($"Project file '{fullPath}' already exists");

        if (IsOpenAt(fullPath))
            return new MagitekResult<ProjectTree>.Failed($"File '{fullPath}' is already open");

        if (!Directory.Exists(directory))
            return new MagitekResult<ProjectTree>.Failed($"Directory '{directory}' does not exist");

        if (FindXmlConflict(directory) is { } xmlConflict)
            return new MagitekResult<ProjectTree>.Failed(xmlConflict);

        var project = new ImageProject(projectName);
        var root = new ProjectNode(directory, project.Name, project)
        {
            DiskLocation = fullPath
        };
        var tree = new ProjectTree(root);

        MagitekResult writeResult;
        try
        {
            writeResult = await _serializerFactory.CreateWriter(tree).WriteResourceAsync(root, true);
        }
        catch (Exception ex)
        {
            writeResult = new MagitekResult.Failed($"Failed to create project: {ex.Message}");
        }

        if (writeResult.HasFailed)
            return new MagitekResult<ProjectTree>.Failed(writeResult.AsError.Reason);

        AddProject(tree);
        ProjectOpened?.Invoke(this, tree);

        return new MagitekResult<ProjectTree>.Success(tree);
    }

    public virtual Task<MagitekResult<ProjectTree>> CreateNewProjectWithExistingFileAsync(string projectFileName, string dataFileName) =>
        WithWriteLockAsync(() => CreateNewProjectWithExistingFileCoreAsync(projectFileName, dataFileName));

    private async Task<MagitekResult<ProjectTree>> CreateNewProjectWithExistingFileCoreAsync(string projectFileName, string dataFileName)
    {
        var fullPath = Path.GetFullPath(projectFileName);

        if (IsOpenAt(fullPath))
            return new MagitekResult<ProjectTree>.Failed($"File '{fullPath}' is already open");

        if (File.Exists(fullPath))
        {
            return new MagitekResult<ProjectTree>.Failed($"Project file '{projectFileName}' already exists");
        }

        if (!File.Exists(dataFileName))
        {
            return new MagitekResult<ProjectTree>.Failed($"Data file '{dataFileName}' does not exist");
        }

        var projectName = Path.GetFileNameWithoutExtension(projectFileName);
        var dataName = Path.GetFileNameWithoutExtension(dataFileName);
        foreach (var name in new[] { projectName, dataName })
        {
            var nameResult = ResourceName.Validate(name, false);
            if (nameResult.HasFailed)
                return new MagitekResult<ProjectTree>.Failed(nameResult.AsError.Reason);
        }

        if (ResourceName.AreSame(projectName, dataName))
            return new MagitekResult<ProjectTree>.Failed($"A resource at the project root cannot share the project's name '{projectName}'");

        var dataPath = Path.GetFullPath(dataFileName);
        var holdingTree = _projects
            .Where(x => !x.IsStandaloneFile)
            .FirstOrDefault(tree => tree.EnumerateDepthFirst().Select(x => x.Item).OfType<FileDataSource>()
                .Any(x => string.Equals(Path.GetFullPath(x.FileLocation), dataPath, StringComparison.OrdinalIgnoreCase)));

        if (holdingTree is not null)
            return new MagitekResult<ProjectTree>.Failed($"'{Path.GetFileName(dataFileName)}' is already in project '{holdingTree.Name}'");

        var project = new ImageProject(projectName);
        var baseDirectory = Path.GetDirectoryName(fullPath)!;

        var root = new ProjectNode(baseDirectory, project.Name, project)
        {
            DiskLocation = fullPath
        };
        var tree = new ProjectTree(root);

        var dataFile = new FileDataSource(dataName, dataFileName);
        var dataNode = new DataFileNode(dataFile.Name, dataFile);
        tree.AttachNodeToPath("", dataNode);

        AddProject(tree);
        var result = await SaveProjectCoreAsync(tree);

        if (result.HasSucceeded)
        {
            ProjectOpened?.Invoke(this, tree);
            return new MagitekResult<ProjectTree>.Success(tree);
        }
        else
        {
            RemoveProject(tree);
            dataFile.Dispose();
            return new MagitekResult<ProjectTree>.Failed(result.AsError.Reason);
        }
    }

    /// <summary>
    /// Opens the specified project and makes it active in the service.
    /// Performs WAL transaction recovery before reading the project.
    /// </summary>
    /// <param name="projectFileName">Project to be opened</param>
    /// <returns>The opened project</returns>
    public virtual async Task<MagitekResults<ProjectTree>> OpenProjectFileAsync(string projectFileName)
    {
        if (string.IsNullOrWhiteSpace(projectFileName))
            throw new ArgumentException($"{nameof(OpenProjectFileAsync)} cannot have a null or empty value for '{nameof(projectFileName)}'");

        if (!File.Exists(projectFileName))
            return new MagitekResults<ProjectTree>.Failed($"File '{projectFileName}' does not exist");

        var fullPath = Path.GetFullPath(projectFileName);
        if (_projects.Any(x => x.Root.DiskLocation is not null &&
            string.Equals(Path.GetFullPath(x.Root.DiskLocation), fullPath, StringComparison.OrdinalIgnoreCase)))
            return new MagitekResults<ProjectTree>.Failed($"File '{projectFileName}' is already open");

        try
        {
            var baseDirectory = Path.GetDirectoryName(fullPath)!;
            var recoveryResult = await WriteAheadLogTransaction.RecoverAsync(baseDirectory);

            if (recoveryResult.HasFailed)
                return new MagitekResults<ProjectTree>.Failed($"Transaction recovery failed for '{projectFileName}': {recoveryResult.AsError.Reason}");

            var reader = _serializerFactory.CreateReader();
            var result = reader.ReadProject(projectFileName);

            return result.Match(
                success =>
                {
                    AddProject(success.Result);
                    ProjectOpened?.Invoke(this, success.Result);
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

    /// <inheritdoc/>
    public virtual MagitekResult<ProjectTree> OpenDataFile(string fileName)
    {
        var path = Path.GetFullPath(fileName);

        var openTree = _projects.FirstOrDefault(x => x.IsStandaloneFile &&
            string.Equals(x.Root.DiskLocation, path, StringComparison.OrdinalIgnoreCase));

        if (openTree is not null)
            return new MagitekResult<ProjectTree>.Success(openTree);

        if (!File.Exists(path))
            return new MagitekResult<ProjectTree>.Failed($"File '{path}' does not exist");

        var name = Path.GetFileName(path);
        var source = new FileDataSource(name, path);
        var root = new DataFileNode(name, source) { DiskLocation = path };
        var tree = new ProjectTree(root);

        AddProject(tree);
        ProjectOpened?.Invoke(this, tree);

        return new MagitekResult<ProjectTree>.Success(tree);
    }

    /// <summary>
    /// Saves the project
    /// </summary>
    /// <param name="projectTree">Project to be saved</param>
    /// <returns></returns>
    public virtual async Task<MagitekResult> SaveProjectAsync(ProjectTree projectTree)
    {
        if (projectTree is null)
            throw new InvalidOperationException($"{nameof(SaveProjectAsync)} parameter '{nameof(projectTree)}' was null");

        return await WithWriteLockAsync(() => SaveProjectCoreAsync(projectTree));
    }

    private async Task<MagitekResult> SaveProjectCoreAsync(ProjectTree projectTree)
    {
        if (projectTree.IsStandaloneFile)
            return MagitekResult.SuccessResult;

        var projectFileLocation = projectTree.Root.DiskLocation;

        if (string.IsNullOrWhiteSpace(projectFileLocation))
            throw new InvalidOperationException($"{nameof(SaveProjectAsync)} cannot have a null or empty value for the project's file location");

        try
        {
            var writer = _serializerFactory.CreateWriter(projectTree);
            return await writer.WriteProjectAsync(projectFileLocation);
        }
        catch (Exception ex)
        {
            return new MagitekResult.Failed($"Failed to save project: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes a copy of the project's XML at another location and switches the open project to it, leaving the old location intact
    /// </summary>
    /// <param name="projectTree">Project to be saved</param>
    /// <param name="projectFileName">New project file; the project is renamed after it</param>
    public virtual async Task<MagitekResult> SaveProjectAsAsync(ProjectTree projectTree, string projectFileName)
    {
        if (projectTree is null)
            throw new InvalidOperationException($"{nameof(SaveProjectAsAsync)} parameter '{nameof(projectTree)}' was null");

        if (string.IsNullOrWhiteSpace(projectFileName))
            throw new ArgumentException($"{nameof(SaveProjectAsAsync)} cannot have a null or empty value for '{nameof(projectFileName)}'");

        return await WithWriteLockAsync(() => SaveProjectAsCoreAsync(projectTree, projectFileName));
    }

    private async Task<MagitekResult> SaveProjectAsCoreAsync(ProjectTree projectTree, string projectFileName)
    {
        if (projectTree.IsStandaloneFile)
            return new MagitekResult.Failed(StandaloneFileReason(projectTree));

        var fullPath = Path.GetFullPath(projectFileName);
        var newDirectory = Path.GetDirectoryName(fullPath)!;
        var newName = Path.GetFileNameWithoutExtension(fullPath);

        if (File.Exists(fullPath))
            return new MagitekResult.Failed($"Project file '{fullPath}' already exists");

        if (Directory.Exists(newDirectory) && FindXmlConflict(newDirectory) is { } xmlConflict)
            return new MagitekResult.Failed(xmlConflict);

        var canRename = CanRenameResource(projectTree.Root, newName);
        if (canRename.HasFailed)
            return canRename;

        var root = (ProjectNode)projectTree.Root;
        var project = (ImageProject)root.Item;
        var oldBaseDirectory = root.BaseDirectory;
        var oldName = root.Name;
        var oldProjectRoot = project.Root;
        var oldModel = root.Model;
        var oldLocations = projectTree.EnumerateDepthFirst().Select(x => (Node: x, x.DiskLocation)).ToList();
        var createdDirectories = new List<string>();

        MagitekResult failure;
        try
        {
            root.BaseDirectory = newDirectory;
            project.Root = "";
            root.DiskLocation = fullPath;
            // Forces the project file write even when the name and root are unchanged
            root.Model = null;

            if (!string.Equals(oldName, newName, StringComparison.Ordinal))
                root.Rename(newName);

            RelocateFolders(projectTree, root);
            var folderDirectories = projectTree.EnumerateDepthFirst().OfType<ResourceFolderNode>().Select(x => x.DiskLocation!);
            foreach (var directory in folderDirectories.Prepend(newDirectory))
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    createdDirectories.Add(directory);
                }
            }

            var writeResult = await _serializerFactory.CreateWriter(projectTree).WriteProjectAsync(fullPath);
            if (writeResult.HasSucceeded)
                return writeResult;

            failure = writeResult;
        }
        catch (Exception ex)
        {
            failure = new MagitekResult.Failed($"Failed to save project: {ex.Message}");
        }

        if (!string.Equals(root.Name, oldName, StringComparison.Ordinal))
            root.Rename(oldName);

        root.BaseDirectory = oldBaseDirectory;
        project.Root = oldProjectRoot;
        root.Model = oldModel;
        foreach (var (node, location) in oldLocations)
            node.DiskLocation = location;

        foreach (var directory in Enumerable.Reverse(createdDirectories))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Leaving an empty directory behind does not affect either project
            }
        }

        return failure;
    }

    /// <summary>
    /// Closes the specified project that is active in the service and frees associated resources
    /// </summary>
    /// <param name="projectTree">Project to be closed</param>
    public virtual void CloseProject(ProjectTree projectTree)
    {
        if (projectTree is null)
            throw new InvalidOperationException($"{nameof(CloseProject)} parameter '{nameof(projectTree)}' was null");

        if (_projects.Contains(projectTree))
        {
            foreach (var resource in projectTree.EnumerateBreadthFirst().Select(x => x.Item).OfType<IDisposable>())
                resource.Dispose();

            RemoveProject(projectTree);
            ProjectClosed?.Invoke(this, projectTree);
        }
    }

    /// <summary>
    /// Closes all projects and frees their associated resources
    /// </summary>
    public virtual void CloseProjects()
    {
        var closedProjects = _projects.ToList();
        var resources = closedProjects.SelectMany(tree => tree.EnumerateDepthFirst().Select(x => x.Item).OfType<IDisposable>());

        foreach (var resource in resources)
            resource.Dispose();

        foreach (var tree in closedProjects)
            RemoveProject(tree);

        foreach (var tree in closedProjects)
            ProjectClosed?.Invoke(this, tree);
    }

    /// <summary>
    /// Adds the specified resource to the parent resource node
    /// </summary>
    /// <param name="parentNode">ResourceNode that is contained by the project</param>
    /// <param name="resource">New resource to add</param>
    /// <returns>The added resource node result</returns>
    public virtual Task<MagitekResult<ResourceNode>> AddResourceAsync(ResourceNode parentNode, IProjectResource resource) =>
        WithWriteLockAsync(() => AddResourceCoreAsync(parentNode, resource));

    private async Task<MagitekResult<ResourceNode>> AddResourceCoreAsync(ResourceNode parentNode, IProjectResource resource)
    {
        var canAdd = CanAddResource(parentNode, resource.Name, resource is ResourceFolder);
        if (canAdd.HasFailed)
            return new MagitekResult<ResourceNode>.Failed(canAdd.AsError.Reason);

        var tree = _projects.First(x => x.ContainsNode(parentNode));

        try
        {
            ResourceNode? childNode = resource switch
            {
                CompressedDataSource compressed => new CompressedDataNode(compressed.Name, compressed),
                TilemapArranger tilemap => new TilemapNode(tilemap.Name, tilemap),
                MetatileSet metatileSet => new MetatileSetNode(metatileSet.Name, metatileSet),
                MetatileMap metatileMap => new MetatileMapNode(metatileMap.Name, metatileMap),
                DataSource df => new DataFileNode(df.Name, df),
                ScatteredArranger arranger => new ArrangerNode(arranger.Name, arranger),
                Palette pal => new PaletteNode(pal.Name, pal),
                ResourceFolder folder => new ResourceFolderNode(folder.Name, folder),
                _ => null
            };

            if (childNode is null)
                return new MagitekResult<ResourceNode>.Failed($"Cannot add a resource of type '{resource.GetType()}'");

            childNode.DiskLocation = ResourceFileLocator.LocateByParent(tree, parentNode, childNode);
            var writeResult = await _serializerFactory.CreateWriter(tree).WriteResourceAsync(childNode, true);
            if (writeResult.HasFailed)
                return new MagitekResult<ResourceNode>.Failed(writeResult.AsError.Reason);

            parentNode.AttachChildNode(childNode);

            // Raises the project file's version with the first tilemap or compressed data; a failed write is retried by the next project save
            if (tree.Root is ProjectNode)
                await _serializerFactory.CreateWriter(tree).WriteResourceAsync(tree.Root, false);

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
    /// <param name="name">Name of the folder, used as given</param>
    /// <returns>The newly created ResourceNode result</returns>
    public virtual Task<MagitekResult<ResourceNode>> CreateNewFolderAsync(ResourceNode parentNode, string name) =>
        WithWriteLockAsync(() => Task.FromResult(CreateNewFolderCore(parentNode, name)));

    private MagitekResult<ResourceNode> CreateNewFolderCore(ResourceNode parentNode, string name)
    {
        var canAdd = CanAddResource(parentNode, name, true);
        if (canAdd.HasFailed)
            return new MagitekResult<ResourceNode>.Failed(canAdd.AsError.Reason);

        var tree = _projects.First(x => x.ContainsNode(parentNode));

        try
        {
            var folder = new ResourceFolder(name);
            var node = new ResourceFolderNode(name, folder);
            var directoryName = ResourceFileLocator.LocateByParent(tree, parentNode, node);
            node.DiskLocation = directoryName;

            Directory.CreateDirectory(directoryName);
            parentNode.AttachChildNode(node);

            return new MagitekResult<ResourceNode>.Success(node);
        }
        catch (Exception ex)
        {
            return new MagitekResult<ResourceNode>.Failed($"Could not create folder '{name}' under parent '{parentNode.Name}'\n{ex.Message}");
        }
    }

    /// <inheritdoc/>
    public virtual MagitekResult CanAddResource(ResourceNode parentNode, string name, bool isFolder)
    {
        var tree = _projects.FirstOrDefault(x => x.ContainsNode(parentNode));

        if (tree is null)
            return new MagitekResult.Failed($"{parentNode.Item.Name} is not contained within any loaded project");

        if (!parentNode.Item.CanContainChildResources)
            return new MagitekResult.Failed($"'{parentNode.Name}' cannot contain children");

        var nameResult = ResourceName.Validate(name, isFolder);
        if (nameResult.HasFailed)
            return nameResult;

        return FindNameConflict(tree, parentNode, name, isFolder, null) is { } conflict
            ? new MagitekResult.Failed(conflict)
            : MagitekResult.SuccessResult;
    }

    /// <inheritdoc/>
    public virtual MagitekResult CanRenameResource(ResourceNode node, string newName)
    {
        var tree = _projects.FirstOrDefault(x => x.ContainsNode(node));

        if (tree is null)
            return new MagitekResult.Failed($"Could not locate '{node.Name}' in any loaded project");

        if (tree.IsStandaloneFile)
            return new MagitekResult.Failed(StandaloneFileReason(tree));

        if (string.Equals(node.Name, newName, StringComparison.Ordinal))
            return MagitekResult.SuccessResult;

        var isFolder = node is ResourceFolderNode;
        var nameResult = ResourceName.Validate(newName, isFolder);
        if (nameResult.HasFailed)
            return nameResult;

        if (node is ProjectNode)
        {
            var clash = node.ChildNodes.FirstOrDefault(x => x is not ResourceFolderNode && ResourceName.AreSame(x.Name, newName));
            return clash is null
                ? MagitekResult.SuccessResult
                : new MagitekResult.Failed($"The project cannot share a name with its root-level resource '{clash.Name}'");
        }

        return FindNameConflict(tree, node.Parent!, newName, isFolder, node) is { } conflict
            ? new MagitekResult.Failed(conflict)
            : MagitekResult.SuccessResult;
    }

    private static string? FindNameConflict(ProjectTree tree, ResourceNode parent, string name, bool isFolder, ResourceNode? ignore)
    {
        var sibling = parent.ChildNodes.FirstOrDefault(x => !ReferenceEquals(x, ignore) && ResourceName.AreSame(x.Name, name));
        if (sibling is not null)
            return $"'{parent.Name}' already contains '{sibling.Name}'";

        // The project file shares the base directory with root-level resource files
        if (parent is ProjectNode && !isFolder && ResourceName.AreSame(name, tree.Name))
            return $"A resource at the project root cannot share the project's name '{tree.Name}'";

        return null;
    }

    /// <summary>
    /// Persists a single Project Resource's schema
    /// </summary>
    /// <param name="projectTree">Project containing resourceNode</param>
    /// <param name="resourceNode">Resource to persist</param>
    /// <param name="alwaysOverwrite">Persist the resource even if unchanged</param>
    /// <returns>The result of the operation</returns>
    public virtual async Task<MagitekResult> SaveResourceAsync(ProjectTree projectTree, ResourceNode resourceNode, bool alwaysOverwrite)
    {
        if (projectTree is null)
            throw new InvalidOperationException($"{nameof(SaveResourceAsync)} parameter '{nameof(projectTree)}' was null");

        return await WithWriteLockAsync(() => SaveResourceCoreAsync(projectTree, resourceNode, alwaysOverwrite));
    }

    private async Task<MagitekResult> SaveResourceCoreAsync(ProjectTree projectTree, ResourceNode resourceNode, bool alwaysOverwrite)
    {
        if (projectTree.IsStandaloneFile)
            return new MagitekResult.Failed(StandaloneFileReason(projectTree));

        if (projectTree.Root.DiskLocation is null)
            throw new InvalidOperationException($"{nameof(SaveResourceAsync)}: '{nameof(projectTree)}' has no disk location");

        string projectFileLocation = projectTree.Root.DiskLocation;

        if (string.IsNullOrWhiteSpace(projectFileLocation))
            throw new InvalidOperationException($"{nameof(SaveResourceAsync)} cannot have a null or empty value for the project's file location");

        try
        {
            var writer = _serializerFactory.CreateWriter(projectTree);
            return await writer.WriteResourceAsync(resourceNode, alwaysOverwrite);
        }
        catch (Exception ex)
        {
            return new MagitekResult.Failed($"Failed to save project resource '{resourceNode.Name}': {ex.Message}");
        }
    }

    public virtual ProjectTree GetContainingProject(ResourceNode node)
    {
        return _projects.FirstOrDefault(x => x.ContainsNode(node)) ??
            throw new ArgumentException($"{nameof(GetContainingProject)} could not locate the node '{node.Name}'");
    }

    public virtual ProjectTree GetContainingProject(IProjectResource resource)
    {
        return _projects.FirstOrDefault(x => x.ContainsResource(resource)) ??
            throw new ArgumentException($"{nameof(GetContainingProject)} could not locate the resource '{resource.Name}'");
    }

    public virtual ProjectTree? FindContainingProject(IProjectResource resource) =>
        _projects.FirstOrDefault(x => x.ContainsResource(resource));

    public virtual bool AreResourcesInSameProject(IProjectResource a, IProjectResource b)
    {
        var projectA = _projects.FirstOrDefault(x => x.ContainsResource(a));
        var projectB = _projects.FirstOrDefault(x => x.ContainsResource(b));

        return projectA is not null && ReferenceEquals(projectA, projectB);
    }

    /// <summary>
    /// Renames a node to the specified name and updates its location and contents on disk
    /// </summary>
    /// <param name="node">Node to be renamed. Must be attached to a loaded project.</param>
    /// <param name="newName">New name</param>
    public virtual Task<MagitekResult> RenameResourceAsync(ResourceNode node, string newName) =>
        WithWriteLockAsync(() => RenameResourceCoreAsync(node, newName));

    private async Task<MagitekResult> RenameResourceCoreAsync(ResourceNode node, string newName)
    {
        var canRename = CanRenameResource(node, newName);
        if (canRename.HasFailed)
            return canRename;

        if (string.Equals(node.Name, newName, StringComparison.Ordinal))
            return MagitekResult.SuccessResult;

        var tree = _projects.First(x => x.ContainsNode(node));
        var oldName = node.Name;

        if (node is ResourceFolderNode) // Exclusively on disk as part of the filesystem
        {
            Guard.IsNotNull(node.DiskLocation);
            Guard.IsNotNull(tree.Root.DiskLocation);
            string oldLocation = node.DiskLocation;
            node.Rename(newName);
            string newLocation = ResourceFileLocator.Locate(tree, node);

            try
            {
                Directory.Move(oldLocation, newLocation);
                RelocateFolders(tree, node);
            }
            catch (Exception ex)
            {
                node.Rename(oldName);
                return new MagitekResult.Failed($"Could not rename '{oldName}': {ex.Message}");
            }

            var serializer = _serializerFactory.CreateWriter(tree);
            var writeResult = await serializer.WriteProjectAsync(tree.Root.DiskLocation);
            return writeResult.Match<MagitekResult>(
                success => MagitekResult.SuccessResult,
                failed =>
                {
                    node.Rename(oldName);
                    Directory.Move(newLocation, oldLocation);
                    RelocateFolders(tree, node);
                    return failed;
                });
        }
        else if (node.DiskLocation is not null) // On disk
        {
            Guard.IsNotNull(tree.Root.DiskLocation);
            string oldLocation = node.DiskLocation;
            node.Rename(newName);

            MagitekResult writeResult;
            try
            {
                var serializer = _serializerFactory.CreateWriter(tree);
                writeResult = await serializer.WriteProjectAsync(tree.Root.DiskLocation);
            }
            catch (Exception ex)
            {
                writeResult = new MagitekResult.Failed($"Could not rename '{oldName}': {ex.Message}");
            }

            if (writeResult.HasFailed)
            {
                node.Rename(oldName);
                return writeResult;
            }

            if (node.DiskLocation is string newLocation && !string.Equals(oldLocation, newLocation, StringComparison.Ordinal))
                RemoveRenamedFile(oldLocation, newLocation);
        }
        else if (node.DiskLocation is null) // In-memory only
        {
            node.Rename(newName);
        }

        return MagitekResult.SuccessResult;
    }

    private static void RemoveRenamedFile(string oldLocation, string newLocation)
    {
        if (!string.Equals(oldLocation, newLocation, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(oldLocation);
            return;
        }

        var fileNames = Directory.EnumerateFiles(Path.GetDirectoryName(newLocation)!)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);
        bool hasOldFile = fileNames.Contains(Path.GetFileName(oldLocation));
        bool hasNewFile = fileNames.Contains(Path.GetFileName(newLocation));

        // On case-insensitive filesystems the write lands in the old file and keeps its casing
        if (hasOldFile && hasNewFile)
            File.Delete(oldLocation);
        else if (hasOldFile)
        {
            var tempLocation = $"{newLocation}.{Guid.NewGuid():N}.tmp";
            File.Move(oldLocation, tempLocation);
            File.Move(tempLocation, newLocation);
        }
    }

    private static void RelocateFolders(ProjectTree tree, ResourceNode folderNode)
    {
        foreach (var folder in folderNode.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>().OfType<ResourceFolderNode>())
            folder.DiskLocation = ResourceFileLocator.Locate(tree, folder);
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
            return new MagitekResult.Failed($"{node.Name} has no parent and may be a root node");

        if (ReferenceEquals(node, parentNode))
            return new MagitekResult.Failed($"Cannot move {node.Name} onto itself");

        var nodeKey = tree.CreatePathKey(node);
        var parentKey = tree.CreatePathKey(parentNode);

        if (tree.CreatePathKey(node.Parent) == parentKey)
            return new MagitekResult.Failed($"Cannot move {node.Name} onto itself");

        if (FindNameConflict(tree, parentNode, node.Name, node is ResourceFolderNode, null) is { } conflict)
            return new MagitekResult.Failed(conflict);

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

        if (node.DiskLocation is null)
            return new MagitekResult.Failed($"{nodeKey} cannot be moved because it does not have a location on disk");

        return MagitekResult.SuccessResult;
    }

    /// <summary>
    /// Moves the specified node to be a child of the specified parent, if possible
    /// Node must be moved within the same project and the root node may not be moved
    /// </summary>
    /// <param name="node">Node to move</param>
    /// <param name="parentNode">Parent destination</param>
    /// <returns></returns>
    public virtual async Task<MagitekResult> MoveNodeAsync(ResourceNode node, ResourceNode parentNode)
    {
        Guard.IsNotNull(node);
        Guard.IsNotNull(parentNode);

        return await WithWriteLockAsync(() => MoveNodeCoreAsync(node, parentNode));
    }

    private async Task<MagitekResult> MoveNodeCoreAsync(ResourceNode node, ResourceNode parentNode)
    {
        var canMoveResult = CanMoveNode(node, parentNode);
        if (canMoveResult.HasFailed)
            return canMoveResult;

        var tree = GetContainingProject(node);

        var serializer = _serializerFactory.CreateWriter(tree);
        var newLocation = ResourceFileLocator.LocateByParent(tree, parentNode, node);
        var oldLocation = node.DiskLocation;
        var oldParent = node.Parent;

        Guard.IsNotNull(oldLocation);
        Guard.IsNotNull(newLocation);
        Guard.IsNotNull(tree.Root.DiskLocation);

        var isFolder = node is ResourceFolderNode;

        try
        {
            if (isFolder)
                Directory.Move(oldLocation, newLocation);
            else
                File.Move(oldLocation, newLocation);
        }
        catch (Exception ex)
        {
            return new MagitekResult.Failed($"Failed to move node '{node.Name}': {ex.Message}");
        }

        MagitekResult writeResult;
        try
        {
            node.DiskLocation = newLocation;
            node.MoveTo(parentNode);
            if (isFolder)
                RelocateFolders(tree, node);

            writeResult = await serializer.WriteProjectAsync(tree.Root.DiskLocation);
        }
        catch (Exception ex)
        {
            writeResult = new MagitekResult.Failed($"Failed to move node '{node.Name}': {ex.Message}");
        }

        if (writeResult.HasFailed)
        {
            if (isFolder)
                Directory.Move(newLocation, oldLocation);
            else
                File.Move(newLocation, oldLocation);

            node.DiskLocation = oldLocation;
            if (oldParent is not null && !ReferenceEquals(node.Parent, oldParent))
                node.MoveTo(oldParent);
            if (isFolder)
                RelocateFolders(tree, node);
        }

        return writeResult;
    }

    /// <summary>
    /// Repairs a missing data file by copying <paramref name="sourceFileName"/> to the location the project expects,
    /// so no project references change
    /// </summary>
    public virtual Task<MagitekResult> RelinkDataFileAsync(FileDataSource dataSource, string sourceFileName)
    {
        Guard.IsNotNull(dataSource);
        Guard.IsNotNullOrWhiteSpace(sourceFileName);

        if (!dataSource.IsMissing)
            return Task.FromResult<MagitekResult>(new MagitekResult.Failed($"'{dataSource.Name}' is not missing"));

        if (!File.Exists(sourceFileName))
            return Task.FromResult<MagitekResult>(new MagitekResult.Failed($"'{sourceFileName}' does not exist"));

        try
        {
            var directory = Path.GetDirectoryName(dataSource.FileLocation);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.Copy(sourceFileName, dataSource.FileLocation);
        }
        catch (Exception ex)
        {
            return Task.FromResult<MagitekResult>(new MagitekResult.Failed($"Could not relink '{dataSource.Name}': {ex.Message}"));
        }

        dataSource.Reopen();

        var palettes = FindContainingProject(dataSource)?.Root
            .SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
            .Select(x => x.Item)
            .OfType<Palette>()
            .Where(x => ReferenceEquals(x.DataSource, dataSource)) ?? [];

        foreach (var palette in palettes)
            palette.Reload();

        dataSource.NotifyDataWritten();
        return Task.FromResult<MagitekResult>(MagitekResult.SuccessResult);
    }

    /// <summary>
    /// Applies deletion and modification changes to the containing tree and disk
    /// </summary>
    /// <param name="plan">Plan created by <see cref="PreviewResourceDeletion"/></param>
    /// <param name="defaultPalette">Default palette to fallback to when a resource loses a palette</param>
    public virtual Task<MagitekResult> ApplyResourceDeletionAsync(ResourceDeletionPlan plan, Palette defaultPalette) =>
        WithWriteLockAsync(() => ApplyResourceDeletionCoreAsync(plan, defaultPalette));

    private async Task<MagitekResult> ApplyResourceDeletionCoreAsync(ResourceDeletionPlan plan, Palette defaultPalette)
    {
        var tree = plan.Tree;
        var removedItems = plan.Changes.Where(x => x.Removed).ToList();
        var removedResources = removedItems.Select(x => x.Resource).ToHashSet();
        var changedArrangers = plan.Changes
            .Where(x => !x.Removed && x.Resource is ScatteredArranger)
            .ToList();
        var changedTilemaps = plan.Changes
            .Where(x => x is { Removed: false, LostTilePalette: true, Resource: TilemapArranger })
            .ToList();

        try
        {
            var resourceMap = tree.CreateResourceMap(_serializerFactory.GlobalResources);
            var removedKeys = removedResources.Where(resourceMap.ContainsKey).Select(x => resourceMap[x]).ToHashSet();
            var fallbackPaletteKey = resourceMap.GetValueOrDefault(defaultPalette);

            var edits = changedArrangers
                .Select(x => (x.ResourceNode, (ResourceModel)WithoutRemovedKeys(((ScatteredArranger)x.Resource).MapToModel(resourceMap), removedKeys, fallbackPaletteKey)))
                .Concat(changedTilemaps.Select(x =>
                    (x.ResourceNode, (ResourceModel)(((TilemapArranger)x.Resource).MapToModel(resourceMap) with { TilePaletteKey = null }))))
                .ToList();

            var writeResult = await _serializerFactory.CreateWriter(tree).WriteModelsAsync(edits);
            if (writeResult.HasFailed)
                return writeResult;
        }
        catch (Exception ex)
        {
            return new MagitekResult.Failed($"Could not update the resources that use the removed resources: {ex.Message}");
        }

        foreach (var arranger in changedArrangers.Select(x => (ScatteredArranger)x.Resource))
        {
            foreach (var source in removedResources.OfType<DataSource>())
                arranger.UnlinkResource(source);

            foreach (var (x, y) in arranger.EnumerateElementsWithinElementRange())
            {
                if (arranger.GetElement(x, y) is ArrangerElement { Codec: IIndexedCodec codec } el && removedResources.Contains(codec.Palette))
                {
                    codec.Palette = defaultPalette;
                    arranger.SetElement(el, x, y);
                }
            }
        }

        foreach (var tilemap in changedTilemaps.Select(x => (TilemapArranger)x.Resource))
            tilemap.TilePaletteArranger = null;

        var undeletableFiles = new List<string>();

        foreach (var item in removedItems.Where(x => x.Resource is not ResourceFolder))
        {
            var resourceParent = item.ResourceNode.Parent;
            resourceParent?.RemoveChildNode(item.Resource.Name);

            if (item.ResourceNode.DiskLocation is string location)
            {
                try
                {
                    File.Delete(location);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    undeletableFiles.Add(location);
                }
            }

            // Editors on the source close on Removed, so nothing reads it after this
            if (item.Resource is IDisposable disposable and (DataSource or TilemapArranger or MetatileMap))
                disposable.Dispose();
        }

        var keptFolders = new List<string>();

        // Longest path first deletes children before their parents
        foreach (var item in removedItems.Where(x => x.Resource is ResourceFolder).OrderByDescending(x => x.ResourceNode.DiskLocation?.Length ?? 0))
        {
            var resourceParent = item.ResourceNode.Parent;
            resourceParent?.RemoveChildNode(item.Resource.Name);

            // Non-empty folders hold files the user placed there, such as ROMs, so they are left on disk
            if (item.ResourceNode.DiskLocation is string location && Directory.Exists(location))
            {
                if (Directory.EnumerateFileSystemEntries(location).Any())
                    keptFolders.Add(location);
                else
                    Directory.Delete(location);
            }
        }

        var reasons = new List<string>();

        if (undeletableFiles.Count > 0)
            reasons.Add($"These resource files could not be deleted. They will reappear when the project is reopened until removed:\n{string.Join("\n", undeletableFiles)}");

        if (keptFolders.Count > 0)
            reasons.Add($"These folders contain other files and were left on disk. They will reappear when the project is reopened until removed:\n{string.Join("\n", keptFolders)}");

        return reasons.Count > 0
            ? new MagitekResult.Failed(string.Join("\n\n", reasons))
            : MagitekResult.SuccessResult;
    }

    private static ScatteredArrangerModel WithoutRemovedKeys(ScatteredArrangerModel model, HashSet<string> removedKeys, string? fallbackPaletteKey)
    {
        var grid = (ArrangerElementModel[,])model.ElementGrid.Clone();

        for (int y = 0; y < grid.GetLength(1); y++)
        {
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                var el = grid[x, y];

                if (el is null)
                    continue;

                if (removedKeys.Contains(el.DataFileKey))
                {
                    grid[x, y] = null!;
                }
                else if (el.PaletteKey is string paletteKey && removedKeys.Contains(paletteKey))
                {
                    grid[x, y] = el with
                    {
                        PaletteKey = fallbackPaletteKey ?? throw new InvalidOperationException("The fallback palette is not available to the project")
                    };
                }
            }
        }

        return model with { ElementGrid = grid };
    }

    /// <summary>
    /// Previews a list of changes/deletions that will happen if the specified node is deleted
    /// </summary>
    /// <param name="deleteNode">Node to preview deletion of</param>
    public virtual MagitekResult<ResourceDeletionPlan> PreviewResourceDeletion(ResourceNode deleteNode)
    {
        Guard.IsNotNull(deleteNode);

        var tree = GetContainingProject(deleteNode);
        if (tree.IsStandaloneFile)
            return new MagitekResult<ResourceDeletionPlan>.Failed(StandaloneFileReason(tree));

        var changes = new List<ResourceChange>();

        var removedDict = deleteNode.SelfAndDescendantsDepthFirst<ResourceNode, IProjectResource>()
            .Select(x => new ResourceChange(x, tree.CreatePathKey(x), true, false, false))
            .ToDictionary(key => key.Resource, val => val);

        changes.AddRange(removedDict.Values);

        // Compressed data over a removed source is removed with it, before palettes and arrangers are checked against the removals
        var compressedNodes = tree.EnumerateDepthFirst().Where(x => x.Item is CompressedDataSource).ToList();
        bool foundCompressed;
        do
        {
            foundCompressed = false;
            foreach (var node in compressedNodes.Where(x => !removedDict.ContainsKey(x.Item) && removedDict.ContainsKey(((CompressedDataSource)x.Item).Parent)))
            {
                var compressedChange = new ResourceChange(node, tree.CreatePathKey(node), true, false, false);
                removedDict[node.Item] = compressedChange;
                changes.Add(compressedChange);
                foundCompressed = true;
            }
        } while (foundCompressed);

        // Palettes with removed DataFiles must be checked early, so that Arrangers are effected in the main loop by removed Palettes
        var removedPaletteNodes = tree.EnumerateDepthFirst()
            .Where(x => x.Item is Palette pal && pal.DataSource is not null)
            .Where(x => removedDict.ContainsKey(((Palette)x.Item).DataSource!) && !removedDict.ContainsKey(x.Item))
            .ToList();

        foreach (var paletteNode in removedPaletteNodes)
        {
            var paletteChange = new ResourceChange(paletteNode, tree.CreatePathKey(paletteNode), true, false, false);
            removedDict[paletteNode.Item] = paletteChange;
            changes.Add(paletteChange);
        }

        // Sets before maps, so a map whose set is removed by a cascade is removed too
        foreach (var node in tree.EnumerateDepthFirst().Where(x => x.Item is TilemapArranger or MetatileSet or MetatileMap).OrderBy(x => x.Item is MetatileMap).ToList())
        {
            if (!removedDict.ContainsKey(node.Item) && node.Item.LinkedResources.Any(removedDict.ContainsKey))
            {
                var linkedChange = new ResourceChange(node, tree.CreatePathKey(node), true, false, false);
                removedDict[node.Item] = linkedChange;
                changes.Add(linkedChange);
            }
        }

        foreach (var node in tree.EnumerateDepthFirst().Where(x => !removedDict.ContainsKey(x.Item)))
        {
            if (node.Item is not Arranger arranger || arranger is TilemapArranger)
                continue;

            var elements = arranger.EnumerateElements().OfType<ArrangerElement>().Where(x => x.Source is not null).ToList();
            var lostElements = elements.Any(x => removedDict.ContainsKey(x.Source));
            var removed = lostElements && elements.All(x => removedDict.ContainsKey(x.Source));
            var lostPalette = !removed && elements.Any(x => !removedDict.ContainsKey(x.Source) &&
                x.Codec is IIndexedCodec codec && removedDict.ContainsKey(codec.Palette));

            if (removed || lostPalette || lostElements)
                changes.Add(new ResourceChange(node, tree.CreatePathKey(node), removed, lostPalette, lostElements));
        }

        // The tile palette is not a linked resource, so losing it changes the tilemap instead of removing it
        var removedResources = changes.Where(x => x.Removed).Select(x => x.Resource).ToHashSet();
        foreach (var node in tree.EnumerateDepthFirst().Where(x => x.Item is TilemapArranger && !removedResources.Contains(x.Item)))
        {
            if (((TilemapArranger)node.Item).TilePaletteArranger is { } tilePalette && removedResources.Contains(tilePalette))
                changes.Add(new ResourceChange(node, tree.CreatePathKey(node), false, false, false, LostTilePalette: true));
        }

        return new MagitekResult<ResourceDeletionPlan>.Success(new ResourceDeletionPlan(tree, changes));
    }

    /// <inheritdoc/>
    public virtual MagitekResult<GenerationPlan> PlanGeneration(ProjectTree tree, string dataFileKey, TableDefinition definition, string targetFolderKey,
        bool overwriteEdited = false)
    {
        Guard.IsNotNull(tree);
        Guard.IsNotNull(definition);

        if (tree.IsStandaloneFile)
            return new MagitekResult<GenerationPlan>.Failed(StandaloneFileReason(tree));

        if (!tree.TryGetNode(dataFileKey, out var dataNode) || dataNode.Item is not DataSource source)
            return new MagitekResult<GenerationPlan>.Failed($"'{dataFileKey}' is not a data file in project '{tree.Name}'");

        var targetResult = ResolveTargetFolder(tree, targetFolderKey);
        if (targetResult.HasFailed)
            return new MagitekResult<GenerationPlan>.Failed(targetResult.AsError.Reason);

        GenerationPlan plan;
        try
        {
            var generator = new TableGenerator(definition, source, tree.CreatePathKey(dataNode), targetResult.AsSuccess.Result, _serializerFactory.MapFormatFactory);
            plan = generator.Plan(tree.Root.DiskLocation!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MagitekResult<GenerationPlan>.Failed($"Could not read '{dataFileKey}': {ex.Message}");
        }

        var manifestResult = ReadManifest(plan);
        if (manifestResult.HasFailed)
            return new MagitekResult<GenerationPlan>.Failed(manifestResult.AsError.Reason);

        if (manifestResult.AsSuccess.Result.FindGeneration(plan.ManifestGeneration.Project, plan.ManifestGeneration.Folder) is not { } generation)
            return new MagitekResult<GenerationPlan>.Success(plan);

        if (!string.Equals(generation.DataFile, plan.DataFileKey, StringComparison.OrdinalIgnoreCase))
            return new MagitekResult<GenerationPlan>.Failed(
                $"'{generation.Folder}' was generated from data file '{generation.DataFile}', not '{plan.DataFileKey}'");

        var entries = FindGeneratedResources(tree, generation);
        var regeneration = KeepReferencedRemovals(tree, RegenerationPlan.Match(plan, entries, overwriteEdited));
        return new MagitekResult<GenerationPlan>.Success(plan with { Regeneration = regeneration });
    }

    private static MagitekResult<TableManifest> ReadManifest(GenerationPlan plan)
    {
        if (!File.Exists(plan.ManifestFileName))
            return new MagitekResult<TableManifest>.Success(TableManifest.Empty);

        var readResult = new XmlTableManifestReader(plan.ManifestSchemaFileName).LoadFromFile(plan.ManifestFileName);
        return readResult.HasFailed
            ? new MagitekResult<TableManifest>.Failed($"Manifest '{plan.ManifestFileName}' could not be read:\n{string.Join("\n", readResult.AsError.Reasons)}")
            : new MagitekResult<TableManifest>.Success(readResult.AsSuccess.Result);
    }

    private static string? GeneratedTypeName(ResourceNode node) => node switch
    {
        CompressedDataNode => "compresseddata",
        PaletteNode => "palette",
        ArrangerNode => "arranger",
        MetatileSetNode => "metatileset",
        MetatileMapNode => "metatilemap",
        TilemapNode => "tilemap",
        _ => null
    };

    // Every entry is looked up by key before any by hash, so a resource found by hash is never one another entry names
    private List<RegenerationEntry> FindGeneratedResources(ProjectTree tree, TableManifestGeneration generation)
    {
        var fileHashes = new Dictionary<ResourceNode, string?>();
        var byHash = new Dictionary<string, ILookup<string?, ResourceNode>>();
        var found = new ResourceNode?[generation.Resources.Count];
        var taken = new HashSet<ResourceNode>();

        for (var i = 0; i < found.Length; i++)
        {
            var entry = generation.Resources[i];
            if (tree.TryGetNode(entry.Key, out var node) && GeneratedTypeName(node) == entry.Type && taken.Add(node))
                found[i] = node;
        }

        for (var i = 0; i < found.Length; i++)
        {
            var entry = generation.Resources[i];
            if (found[i] is not null || entry.Hash is not { } hash)
                continue;

            if (!byHash.TryGetValue(entry.Type, out var candidates))
            {
                candidates = tree.EnumerateDepthFirst().Where(x => GeneratedTypeName(x) == entry.Type).ToLookup(HashOf);
                byHash[entry.Type] = candidates;
            }

            if (candidates[hash].Where(x => !taken.Contains(x)).ToList() is [var moved])
            {
                found[i] = moved;
                taken.Add(moved);
            }
        }

        var writer = _serializerFactory.CreateWriter(tree);
        var entries = new List<RegenerationEntry>();
        var entryKeys = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < found.Length; i++)
        {
            if (found[i] is { } node)
                entryKeys[tree.CreatePathKey(node)] = generation.Resources[i].Key;
        }

        for (var i = 0; i < found.Length; i++)
        {
            var entry = generation.Resources[i];
            if (found[i] is not { } node)
            {
                entries.Add(new RegenerationEntry(entry, null, null, false));
                continue;
            }

            var mapResult = writer.MapCurrentModel(node);
            var current = mapResult.HasSucceeded ? mapResult.AsSuccess.Result : null;
            var isEdited = entry.Hash is null || current is null || current != node.Model
                || HashOf(node) is var fileHash && fileHash != entry.Hash && !IsRewrittenByMoves(current, fileHash, entry.Hash);
            entries.Add(new RegenerationEntry(entry, tree.CreatePathKey(node), current, isEdited));
        }

        return entries;

        // The file is exactly what TileShop writes for the model, and the model with its references put back is the one generated
        bool IsRewrittenByMoves(ResourceModel current, string? fileHash, string entryHash)
        {
            if (TableManifest.HashContents(writer.SerializeModel(current)) != fileHash)
                return false;

            var original = RegenerationPlan.WithKeys(current, current.Name, key => entryKeys.GetValueOrDefault(key, key));
            return TableManifest.HashContents(writer.SerializeModel(original)) == entryHash;
        }

        string? HashOf(ResourceNode node)
        {
            if (fileHashes.TryGetValue(node, out var hash))
                return hash;

            try
            {
                hash = node.DiskLocation is { } location && File.Exists(location) ? TableManifest.HashFile(location) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                hash = null;
            }

            fileHashes[node] = hash;
            return hash;
        }
    }

    // Keeping one removal can block another, such as a palette a kept set uses, so this repeats until nothing more is kept
    private RegenerationPlan KeepReferencedRemovals(ProjectTree tree, RegenerationPlan regeneration)
    {
        var replaced = FindNodes(tree, regeneration.Written.Where(x => x.Kind == RegenerationKind.Changed).Select(x => x.Entry!.CurrentKey)).ToHashSet();
        var written = regeneration.Written.Select(x => (x.TargetKey, Keys: RegenerationPlan.ReferencedKeys(x.Model))).ToList();

        for (var kept = true; kept;)
        {
            kept = false;
            var removedResources = FindNodes(tree, regeneration.Removed.Select(x => x.CurrentKey))
                .Select(x => x.Item)
                .OfType<IProjectResource>()
                .ToHashSet();

            foreach (var removed in regeneration.Removed)
            {
                if (!tree.TryGetNode(removed.CurrentKey!, out var node))
                    continue;

                // A change only cascading through a replaced resource, such as a map whose replaced set loses a palette, does not count
                var changes = PreviewResourceDeletion(node) is { HasSucceeded: true } preview ? preview.AsSuccess.Result.Changes : [];
                var gone = changes.Where(x => x.Removed && !replaced.Contains(x.ResourceNode)).Select(x => x.Resource).ToHashSet();
                var usedBy = changes
                    .Where(x => !removedResources.Contains(x.Resource) && !replaced.Contains(x.ResourceNode) && References(x.Resource, gone.Contains))
                    .Select(x => x.ResourcePath)
                    .ToList();

                usedBy.AddRange(written.Where(x => x.Keys.Contains(removed.CurrentKey!)).Select(x => x.TargetKey));

                if (usedBy.Count > 0)
                {
                    regeneration = regeneration.KeepReferenced(removed, usedBy.Distinct().ToList());
                    kept = true;
                    break;
                }
            }
        }

        return regeneration;
    }

    /// <summary>
    /// Whether <paramref name="resource"/> links to, or uses as its tile palette, a resource matching <paramref name="contains"/>
    /// </summary>
    public static bool References(IProjectResource resource, Func<IProjectResource, bool> contains) =>
        resource.LinkedResources.Any(contains) ||
        resource is TilemapArranger { TilePaletteArranger: { } tilePalette } && contains(tilePalette);

    /// <summary>
    /// The order in which regeneration rebuilds a dependent of a replaced resource, or null when that kind is never rebuilt
    /// </summary>
    public static int? RebuildOrder(object? item) => item switch
    {
        CompressedDataSource => 0,
        Palette => 1,
        MetatileSet => 2,
        TilemapArranger => 3,
        MetatileMap => 4,
        _ => null
    };

    private static IEnumerable<ResourceNode> FindNodes(ProjectTree tree, IEnumerable<string?> keys) =>
        keys.Select(x => x is not null && tree.TryGetNode(x, out var node) ? node : null).OfType<ResourceNode>();

    // Spells the key as the existing folders spell it, so a case variant does not plan a second folder
    private static MagitekResult<string> ResolveTargetFolder(ProjectTree tree, string targetFolderKey)
    {
        ResourceNode? node = tree.Root;
        var key = "";

        foreach (var segment in targetFolderKey.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            var child = node?.ChildNodes.FirstOrDefault(x => ResourceName.AreSame(x.Name, segment));
            if (child is not null and not ResourceFolderNode)
                return new MagitekResult<string>.Failed($"'{tree.CreatePathKey(child)}' is not a folder");

            if (child is null && ResourceName.Validate(segment, true) is { HasFailed: true } invalid)
                return new MagitekResult<string>.Failed($"Target folder segment '{segment}' is not a valid folder name: {invalid.AsError.Reason}");

            key += "/" + (child?.Name ?? segment);
            node = child;
        }

        return new MagitekResult<string>.Success(key.Length == 0 ? "/" : key);
    }

    /// <inheritdoc/>
    public virtual Task<MagitekResult> GenerateResourcesAsync(ProjectTree tree, GenerationPlan plan, bool saveProject = false, bool confirmRemovals = false)
    {
        Guard.IsNotNull(tree);
        Guard.IsNotNull(plan);

        return WithWriteLockAsync(() => GenerateResourcesCoreAsync(tree, plan, saveProject, confirmRemovals));
    }

    private async Task<MagitekResult> GenerateResourcesCoreAsync(ProjectTree tree, GenerationPlan plan, bool saveProject, bool confirmRemovals)
    {
        if (tree.IsStandaloneFile)
            return new MagitekResult.Failed(StandaloneFileReason(tree));

        if (!_projects.Contains(tree))
            return new MagitekResult.Failed($"Project '{tree.Name}' is not open");

        if (!tree.TryGetItem<DataSource>(plan.DataFileKey, out _))
            return new MagitekResult.Failed($"'{plan.DataFileKey}' is not a data file in project '{tree.Name}'");

        var conflicts = FindGenerationConflicts(tree, plan);
        if (conflicts.Count > 0)
            return new MagitekResult.Failed($"Generation would clash with existing resources:\n{string.Join("\n", conflicts)}");

        var manifestResult = ReadManifest(plan);
        if (manifestResult.HasFailed)
            return new MagitekResult.Failed(manifestResult.AsError.Reason);

        if (plan.Regeneration is { } regeneration)
            return await RegenerateCoreAsync(tree, plan, regeneration, manifestResult.AsSuccess.Result, saveProject, confirmRemovals);

        var createdFolders = new List<ResourceNode>();
        var createdDirectories = new List<string>();
        MagitekResult failure;

        try
        {
            foreach (var folderKey in plan.FolderKeys)
                EnsureFolder(tree, folderKey, createdFolders, createdDirectories);

            var buildResult = _serializerFactory.BuildResources(tree, ToBuildModels(tree, plan.Resources.Select(x => (x.Model, x.ParentKey, x.Name))));
            if (buildResult.HasSucceeded)
            {
                var writer = _serializerFactory.CreateWriter(tree);
                var (writeNodes, hashes) = PrepareWrite(tree, writer, plan.Resources.Select(x => x.Key));
                var generation = plan.ManifestGeneration with
                {
                    Resources = plan.ManifestGeneration.Resources.Select(x => x with { Hash = hashes.GetValueOrDefault(x.Key) }).ToList()
                };

                var writeResult = await WriteGenerationAsync(tree, writer, writeNodes, plan.ManifestFileName,
                    manifestResult.AsSuccess.Result.WithGeneration(generation), saveProject);
                if (writeResult.HasSucceeded)
                    return writeResult;

                failure = writeResult;
            }
            else
            {
                failure = new MagitekResult.Failed(string.Join("\n", buildResult.AsError.Reasons));
            }
        }
        catch (Exception ex)
        {
            failure = new MagitekResult.Failed($"Generation failed: {ex.Message}");
        }

        RollBackGeneration(tree, plan.Resources.Select(x => x.Key), createdFolders, createdDirectories);
        return failure;
    }

    private static List<(ResourceModel Model, string ParentNodePath, string FileLocation)> ToBuildModels(ProjectTree tree,
        IEnumerable<(ResourceModel Model, string ParentKey, string Name)> resources) =>
        resources.Select(resource =>
        {
            tree.TryGetNode(resource.ParentKey, out var parent);
            return (resource.Model, resource.ParentKey.TrimStart('/'), ResourceFileLocator.LocateByParent(tree, parent ?? tree.Root, resource.Name, false));
        }).ToList();

    // The builder records each model as persisted; clearing it makes the write include every built resource.
    // The project file is a candidate too, so a format version the new resources need is written.
    private static (List<ResourceNode> Nodes, Dictionary<string, string> Hashes) PrepareWrite(ProjectTree tree, IProjectWriter writer, IEnumerable<string> keys)
    {
        var nodes = new List<ResourceNode> { tree.Root };
        var hashes = new Dictionary<string, string>();

        foreach (var key in keys)
        {
            if (tree.TryGetNode(key, out var node))
            {
                node.Model = null;
                nodes.Add(node);
                hashes[key] = TableManifest.HashContents(writer.SerializeResource(node));
            }
        }

        return (nodes, hashes);
    }

    private static Task<MagitekResult> WriteGenerationAsync(ProjectTree tree, IProjectWriter writer, List<ResourceNode> nodes, string manifestFileName,
        TableManifest manifest, bool saveProject)
    {
        var manifestFiles = new[] { (manifestFileName, XmlTableManifestWriter.Serialize(manifest)) };
        return saveProject
            ? writer.WriteProjectAsync(tree.Root.DiskLocation!, manifestFiles)
            : writer.WriteResourcesAsync(nodes, manifestFiles);
    }

    private async Task<MagitekResult> RegenerateCoreAsync(ProjectTree tree, GenerationPlan plan, RegenerationPlan regeneration, TableManifest manifest,
        bool saveProject, bool confirmRemovals)
    {
        var stale = regeneration.Resources.Select(x => x.Entry?.CurrentKey)
            .Concat(regeneration.Removed.Select(x => x.CurrentKey))
            .OfType<string>()
            .Where(x => !tree.TryGetNode(x, out _))
            .ToList();
        if (stale.Count > 0)
            return new MagitekResult.Failed($"The project changed after regeneration was planned; these resources are gone:\n{string.Join("\n", stale)}");

        if (regeneration.Removed.Count > 0 && !confirmRemovals)
            return new MagitekResult.Failed(
                $"Regeneration would remove {regeneration.Removed.Count} resources:\n{string.Join("\n", regeneration.Removed.Select(x => x.CurrentKey))}");

        var removals = CollectRemovals(tree, regeneration);
        var written = regeneration.Written.ToList();
        var replaced = written
            .Where(x => x.Kind == RegenerationKind.Changed)
            .Select(x => (Resource: x, Node: tree.TryGetNode(x.Entry!.CurrentKey!, out var node) ? node : null!))
            .ToList();
        var oldNodes = replaced.Select(x => x.Node).ToHashSet();
        var detached = new List<(ResourceNode Node, ResourceNode Parent)>();
        var createdFolders = new List<ResourceNode>();
        var createdDirectories = new List<string>();
        MagitekResult failure;

        try
        {
            var usedFolders = written.Select(x => x.ParentKey).ToHashSet(StringComparer.Ordinal);
            foreach (var folderKey in plan.FolderKeys.Where(x => usedFolders.Any(used => used == x || used.StartsWith(x == "/" ? "/" : x + "/", StringComparison.Ordinal))))
                EnsureFolder(tree, folderKey, createdFolders, createdDirectories);

            foreach (var (_, node) in replaced)
            {
                var parent = node.Parent!;
                parent.DetachChildNode(node.Name);
                detached.Add((node, parent));
            }

            var buildResult = _serializerFactory.BuildResources(tree, ToBuildModels(tree, written.Select(x => (x.Model, x.ParentKey, x.Name))));
            if (buildResult.HasSucceeded)
            {
                var writer = _serializerFactory.CreateWriter(tree);
                var (writeNodes, hashes) = PrepareWrite(tree, writer, written.Select(x => x.TargetKey));

                foreach (var resource in regeneration.Resources.Where(x => x.Kind == RegenerationKind.Unchanged))
                {
                    if (tree.TryGetNode(resource.TargetKey, out var node) && node.DiskLocation is { } location && File.Exists(location))
                        hashes[resource.TargetKey] = TableManifest.HashFile(location);
                }

                var writeResult = await WriteGenerationAsync(tree, writer, writeNodes, plan.ManifestFileName,
                    manifest.WithGeneration(regeneration.ToManifestGeneration(plan.ManifestGeneration, hashes)), saveProject);
                if (writeResult.HasSucceeded)
                    return await FinishRegenerationAsync(tree, written, replaced, removals);

                failure = writeResult;
            }
            else
            {
                failure = new MagitekResult.Failed(string.Join("\n", buildResult.AsError.Reasons));
            }
        }
        catch (Exception ex)
        {
            failure = new MagitekResult.Failed($"Regeneration failed: {ex.Message}");
        }

        var builtKeys = written.Select(x => x.TargetKey).Where(x => tree.TryGetNode(x, out var node) && !oldNodes.Contains(node)).ToList();
        RollBackGeneration(tree, builtKeys, createdFolders, createdDirectories);

        foreach (var (node, parent) in Enumerable.Reverse(detached))
            parent.AttachChildNode(node);

        return failure;
    }

    // Previewed again at generation, so the changes hold the nodes as they are now
    private List<ResourceChange> CollectRemovals(ProjectTree tree, RegenerationPlan regeneration)
    {
        var removedNodes = FindNodes(tree, regeneration.Removed.Select(x => x.CurrentKey)).ToHashSet();

        return removedNodes
            .Select(PreviewResourceDeletion)
            .Where(x => x.HasSucceeded)
            .SelectMany(x => x.AsSuccess.Result.Changes)
            .Where(x => x.Removed && removedNodes.Contains(x.ResourceNode))
            .DistinctBy(x => x.ResourceNode)
            .ToList();
    }

    private async Task<MagitekResult> FinishRegenerationAsync(ProjectTree tree, List<RegeneratedResource> written,
        List<(RegeneratedResource Resource, ResourceNode Node)> replaced, List<ResourceChange> removals)
    {
        var reasons = new List<string>();
        var replacements = new Dictionary<IProjectResource, IProjectResource>(ReferenceEqualityComparer.Instance);
        var skipped = removals.Select(x => x.ResourceNode).ToHashSet();

        foreach (var (resource, oldNode) in replaced)
        {
            if (!tree.TryGetNode(resource.TargetKey, out var newNode))
                continue;

            // Users that still name the old key read whatever now holds it, as a reload would, such as a palette added at a renamed one's key
            var atOldKey = tree.TryGetNode(resource.Entry!.CurrentKey!, out var occupant) && occupant.Item.GetType() == oldNode.Item.GetType() ? occupant : newNode;
            replacements[oldNode.Item] = atOldKey.Item;

            if (oldNode.DiskLocation is { } oldLocation && newNode.DiskLocation is { } newLocation && !string.Equals(oldLocation, newLocation, StringComparison.Ordinal))
            {
                try
                {
                    RemoveRenamedFile(oldLocation, newLocation);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    reasons.Add($"'{oldLocation}' could not be deleted and will reappear when the project is reopened: {ex.Message}");
                }
            }
        }

        var retargeted = RetargetReferences(tree, replacements, skipped, reasons);

        if (reasons.Count == 0)
        {
            foreach (var disposable in replacements.Keys.OfType<IDisposable>())
                disposable.Dispose();
        }

        if (retargeted.Count > 0)
        {
            var writeResult = await _serializerFactory.CreateWriter(tree).WriteResourcesAsync(retargeted);
            if (writeResult.HasFailed)
                reasons.Add($"The resources that use regenerated resources could not be saved: {writeResult.AsError.Reason}");
        }

        if (removals.Count > 0)
        {
            var deleteResult = await ApplyResourceDeletionCoreAsync(new ResourceDeletionPlan(tree, removals), _serializerFactory.GlobalResources.OfType<Palette>().First());
            if (deleteResult.HasFailed)
                reasons.Add(deleteResult.AsError.Reason);
        }

        return reasons.Count > 0 ? new MagitekResult.Failed(string.Join("\n\n", reasons)) : MagitekResult.SuccessResult;
    }

    // Returns the changed or rebuilt nodes, whose files may still hold the replaced resources' old keys
    private List<ResourceNode> RetargetReferences(ProjectTree tree, Dictionary<IProjectResource, IProjectResource> replacements,
        HashSet<ResourceNode> skipped, List<string> reasons)
    {
        var retargeted = new List<ResourceNode>();
        if (replacements.Count == 0)
            return retargeted;

        var resourceMap = tree.CreateResourceMap(_serializerFactory.GlobalResources);
        foreach (var (old, replacement) in replacements)
            resourceMap[old] = resourceMap[replacement];

        // Build order, so a rebuilt set's maps are rebuilt in turn
        var rebuildable = tree.EnumerateDepthFirst()
            .Where(x => RebuildOrder(x.Item) is not null && !skipped.Contains(x))
            .OrderBy(x => RebuildOrder(x.Item))
            .ToList();
        var writer = _serializerFactory.CreateWriter(tree);

        foreach (var node in rebuildable)
        {
            if (!node.Item.LinkedResources.Any(replacements.ContainsKey))
                continue;

            var key = tree.CreatePathKey(node);
            var mapResult = writer.MapCurrentModel(node, resourceMap);
            if (mapResult.HasFailed)
            {
                reasons.Add($"'{key}' still uses the replaced resources: {mapResult.AsError.Reason}");
                continue;
            }

            var model = mapResult.AsSuccess.Result;
            var parent = node.Parent!;
            parent.DetachChildNode(node.Name);
            var buildResult = _serializerFactory.BuildResources(tree,
                [(model, parent is ProjectNode ? "" : tree.CreatePathKey(parent).TrimStart('/'), node.DiskLocation!)]);

            if (buildResult.HasFailed || !tree.TryGetNode(key, out var rebuilt))
            {
                if (tree.TryGetNode(key, out var partial))
                    partial.Parent?.RemoveChildNode(partial.Name);

                parent.AttachChildNode(node);
                reasons.Add($"'{key}' still uses the replaced resources: {(buildResult.HasFailed ? string.Join("; ", buildResult.AsError.Reasons) : "it was not rebuilt")}");
                continue;
            }

            rebuilt.Model = node.Model;
            replacements[node.Item] = rebuilt.Item;
            resourceMap[node.Item] = key;
            resourceMap[rebuilt.Item] = key;
            retargeted.Add(rebuilt);
        }

        foreach (var node in tree.EnumerateDepthFirst().Where(x => !skipped.Contains(x)))
        {
            if (node.Item is TilemapArranger { TilePaletteArranger: { } tilePalette } tilemap && replacements.TryGetValue(tilePalette, out var newTilePalette))
            {
                tilemap.TilePaletteArranger = (ScatteredArranger)newTilePalette;
                retargeted.Add(node);
            }
            else if (node.Item is ScatteredArranger arranger && RetargetElements(arranger, replacements))
            {
                retargeted.Add(node);
            }
        }

        return retargeted;
    }

    private static bool RetargetElements(ScatteredArranger arranger, Dictionary<IProjectResource, IProjectResource> replacements)
    {
        var changed = false;

        foreach (var (x, y) in arranger.EnumerateElementsWithinElementRange())
        {
            if (arranger.GetElement(x, y) is not ArrangerElement el)
                continue;

            var source = replacements.TryGetValue(el.Source, out var newSource) ? (DataSource)newSource : el.Source;
            var paletteReplaced = el.Codec is IIndexedCodec { Palette: { } palette } && replacements.ContainsKey(palette);
            if (ReferenceEquals(source, el.Source) && !paletteReplaced)
                continue;

            if (paletteReplaced)
            {
                var codec = (IIndexedCodec)el.Codec;
                codec.Palette = (Palette)replacements[codec.Palette];
            }

            arranger.SetElement(el.WithFile(source, el.SourceAddress), x, y);
            changed = true;
        }

        return changed;
    }

    /// <inheritdoc/>
    public virtual IReadOnlyList<string> FindGenerationConflicts(ProjectTree tree, GenerationPlan plan)
    {
        Guard.IsNotNull(tree);
        Guard.IsNotNull(plan);

        var conflicts = new List<string>();
        var folderKeys = plan.FolderKeys.ToHashSet(StringComparer.Ordinal);
        var resourceKeys = plan.Resources.Select(x => x.Key);
        var movingAway = new HashSet<ResourceNode>();

        // A regeneration checks only the keys it writes to that are new, and a changed resource that moves frees its old key
        if (plan.Regeneration is { } regeneration)
        {
            var moving = regeneration.Written.Where(x => x.Kind == RegenerationKind.Added || x.TargetKey != x.Entry?.CurrentKey).ToList();
            resourceKeys = moving.Select(x => x.TargetKey);
            movingAway = FindNodes(tree, moving.Select(x => x.Entry?.CurrentKey)).ToHashSet();
        }

        foreach (var key in plan.FolderKeys.Concat(resourceKeys))
        {
            var isFolder = folderKeys.Contains(key);
            var segments = key.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                continue;

            ResourceNode? parent = tree.Root;
            foreach (var segment in segments[..^1])
                parent = parent?.ChildNodes.FirstOrDefault(x => x.Name == segment);

            if (parent is null)
                continue;

            var name = segments[^1];
            var sibling = parent.ChildNodes.FirstOrDefault(x => ResourceName.AreSame(x.Name, name) && !movingAway.Contains(x));
            if (sibling is not null && !(isFolder && sibling is ResourceFolderNode && sibling.Name == name))
                conflicts.Add($"'{key}' clashes with '{tree.CreatePathKey(sibling)}'");
            else if (!isFolder && parent is ProjectNode && ResourceName.AreSame(name, tree.Name))
                conflicts.Add($"'{key}' cannot share the project's name '{tree.Name}'");
        }

        return conflicts;
    }

    private static void EnsureFolder(ProjectTree tree, string folderKey, List<ResourceNode> createdFolders, List<string> createdDirectories)
    {
        ResourceNode parent = tree.Root;

        foreach (var segment in folderKey.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (parent.ChildNodes.FirstOrDefault(x => x.Name == segment) is { } existing)
            {
                parent = existing;
                continue;
            }

            var node = new ResourceFolderNode(segment, new ResourceFolder(segment));
            node.DiskLocation = ResourceFileLocator.LocateByParent(tree, parent, node);

            if (!Directory.Exists(node.DiskLocation))
            {
                Directory.CreateDirectory(node.DiskLocation);
                createdDirectories.Add(node.DiskLocation);
            }

            parent.AttachChildNode(node);
            createdFolders.Add(node);
            parent = node;
        }
    }

    private static void RollBackGeneration(ProjectTree tree, IEnumerable<string> keys, List<ResourceNode> createdFolders, List<string> createdDirectories)
    {
        var created = createdFolders.ToHashSet();

        foreach (var key in keys.Reverse())
        {
            if (!tree.TryGetNode(key, out var node))
                continue;

            if (node.Parent is { } parent && !created.Contains(parent))
                parent.RemoveChildNode(node.Name);

            if (node.Item is IDisposable disposable)
                disposable.Dispose();
        }

        foreach (var folder in Enumerable.Reverse(createdFolders))
        {
            if (folder.Parent is { } parent && !created.Contains(parent))
                parent.RemoveChildNode(folder.Name);
        }

        foreach (var directory in Enumerable.Reverse(createdDirectories))
        {
            try
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // An empty directory left behind loads as an empty folder and loses nothing
            }
        }
    }
}
