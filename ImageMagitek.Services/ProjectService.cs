using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Project.Serialization;
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

    private static string? FindXmlConflict(string directory) =>
        Directory.EnumerateFiles(directory, "*.xml", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Any()
            ? $"'{directory}' or one of its subdirectories already holds XML files, which would be loaded as part of the project"
            : null;

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

        if (_projects.Any(x => x.Root.DiskLocation == projectFileName))
            return new MagitekResults<ProjectTree>.Failed($"File '{projectFileName}' is already open");

        try
        {
            var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(projectFileName))!;
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

        return ReferenceEquals(projectA, projectB);
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

        try
        {
            var resourceMap = CreateResourceMap(tree);
            var removedKeys = removedResources.Where(resourceMap.ContainsKey).Select(x => resourceMap[x]).ToHashSet();
            var fallbackPaletteKey = resourceMap.GetValueOrDefault(defaultPalette);

            var edits = changedArrangers
                .Select(x => (x.ResourceNode, (ResourceModel)WithoutRemovedKeys(((ScatteredArranger)x.Resource).MapToModel(resourceMap), removedKeys, fallbackPaletteKey)))
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
            if (item.Resource is DataSource source)
                source.Dispose();
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
        var grid = model.ElementGrid;

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
                    grid[x, y] = new ArrangerElementModel
                    {
                        DataFileKey = el.DataFileKey,
                        PaletteKey = fallbackPaletteKey ?? throw new InvalidOperationException("The fallback palette is not available to the project"),
                        CodecName = el.CodecName,
                        FileAddress = el.FileAddress,
                        PositionX = el.PositionX,
                        PositionY = el.PositionY,
                        Mirror = el.Mirror,
                        Rotation = el.Rotation
                    };
                }
            }
        }

        return model;
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

        foreach (var node in tree.EnumerateDepthFirst().Where(x => !removedDict.ContainsKey(x.Item)))
        {
            if (node.Item is not Arranger arranger)
                continue;

            var elements = arranger.EnumerateElements().OfType<ArrangerElement>().Where(x => x.Source is not null).ToList();
            var lostElements = elements.Any(x => removedDict.ContainsKey(x.Source));
            var removed = lostElements && elements.All(x => removedDict.ContainsKey(x.Source));
            var lostPalette = !removed && elements.Any(x => !removedDict.ContainsKey(x.Source) &&
                x.Codec is IIndexedCodec codec && removedDict.ContainsKey(codec.Palette));

            if (removed || lostPalette || lostElements)
                changes.Add(new ResourceChange(node, tree.CreatePathKey(node), removed, lostPalette, lostElements));
        }

        return new MagitekResult<ResourceDeletionPlan>.Success(new ResourceDeletionPlan(tree, changes));
    }

    /// <summary>
    /// Finds all resources with stale key references to the provided resources
    /// </summary>
    private static IEnumerable<ResourceNode> FindStaleKeyResources(ProjectTree tree, HashSet<IProjectResource> keyChangedResources)
    {
        return tree.Root.SelfAndDescendantsBreadthFirst<ResourceNode, IProjectResource>()
            .Where(x => x.Item.LinkedResources.Any(x => keyChangedResources.Contains(x)));
    }

    private Dictionary<IProjectResource, string> CreateResourceMap(ProjectTree tree)
    {
        var resourceMap = new Dictionary<IProjectResource, string>();

        foreach (var resource in _serializerFactory.GlobalResources)
            resourceMap.Add(resource, resource.Name);

        foreach (var node in tree.EnumerateDepthFirst().Where(x => x is not ResourceFolderNode))
            resourceMap.Add(node.Item, tree.CreatePathKey(node));

        return resourceMap;
    }
}
