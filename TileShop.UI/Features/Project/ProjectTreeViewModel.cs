using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Services;
using Monaco.PathTree;
using Serilog;
using TileShop.Shared.Messages;
using TileShop.Shared.Services;
using TileShop.Shared.Interactions;
using ImageMagitek.Services.Stores;
using TileShop.Shared.Models;

namespace TileShop.UI.ViewModels;

public partial class ProjectTreeViewModel : ObservableRecipient
{
    private readonly IProjectService _projectService;
    private readonly IColorFactory _colorFactory;
    private readonly PaletteStore _paletteStore;
    private readonly IAsyncFileRequestService _fileSelect;
    private readonly IInteractionService _interactions;
    private readonly UserPreferencesStore _preferencesStore;
    private readonly IExploreService _diskExploreService;
    private readonly EditorsViewModel _editors;

    public ProjectTreeViewModel(IProjectService solutionService, IColorFactory colorFactory, PaletteStore paletteStore,
        IAsyncFileRequestService fileSelect, IInteractionService interactionService,
        UserPreferencesStore preferencesStore, IExploreService diskExploreService, EditorsViewModel editors)
    {
        _projectService = solutionService;
        _colorFactory = colorFactory;
        _paletteStore = paletteStore;
        _fileSelect = fileSelect;
        _interactions = interactionService;
        _preferencesStore = preferencesStore;
        _diskExploreService = diskExploreService;
        _editors = editors;

        Messenger.Register<AddScatteredArrangerFromCopyMessage>(this, (r, m) => ReceiveAsync(m));
        Messenger.Register<ImportImageIntoArrangerMessage>(this, async (r, m) => await ImportArrangerInto(m.Arranger, m.Bounds));
        _projectService.ProjectOpened += OnProjectOpened;
        _projectService.ProjectClosed += OnProjectClosed;
    }

    public bool HasProject => Projects.Any();

    private void OnProjectOpened(object? sender, ProjectTree tree)
    {
        Projects.Add(tree.IsStandaloneFile ? new StandaloneFileNodeViewModel(tree.Root) : new ProjectNodeViewModel(tree));
        OnPropertyChanged(nameof(HasProject));
    }

    private void OnProjectClosed(object? sender, ProjectTree tree)
    {
        Projects.Remove(Projects.First(x => ReferenceEquals(x.Node, tree.Root)));
        OnPropertyChanged(nameof(HasProject));
    }

    private ResourceNodeViewModel? FindViewModel(ResourceNode node) =>
        Projects.Select(x => x.Find(node)).FirstOrDefault(x => x is not null);

    [ObservableProperty] private ObservableCollection<ResourceNodeViewModel> _projects = new();
    [ObservableProperty] private ResourceNodeViewModel? _selectedNode;

    [RelayCommand]
    public async Task ActivateSelectedNode()
    {
        if (SelectedNode is ProjectNodeViewModel or FolderNodeViewModel)
        {
            SelectedNode.IsExpanded ^= true;
        }
        else if (SelectedNode?.Node?.Item is not null)
        {
            await _editors.ActivateEditor(SelectedNode.Node.Item);
        }
    }

    [RelayCommand]
    public async Task AddNewFolder(ResourceNodeViewModel parentNodeModel)
    {
        var parent = parentNodeModel.Node;
        var dialogModel = new NameResourceViewModel("New Folder", FindFreeFolderName(parent),
            name => _projectService.CanAddResource(parent, name, true));

        if (await _interactions.RequestAsync(dialogModel) is not { } folderName)
            return;

        var result = await _projectService.CreateNewFolderAsync(parent, folderName);

        await result.Match(
            success =>
            {
                SelectedNode = FindViewModel(success.Result);
                return Task.CompletedTask;
            },
            async fail =>
            {
                await _interactions.AlertAsync("Folder Creation Error", fail.Reason);
            });
    }

    private static string FindFreeFolderName(ResourceNode parent) =>
        Enumerable.Range(1, int.MaxValue - 1)
            .Select(n => n == 1 ? "New Folder" : $"New Folder ({n})")
            .First(name => !parent.ChildNodes.Any(x => ResourceName.AreSame(x.Name, name)));

    [RelayCommand]
    public async Task AddNewDataFile(ResourceNodeViewModel parentNodeModel)
    {
        var dataFileName = await _fileSelect.RequestExistingDataFileName();

        if (dataFileName is not null)
        {
            var dfName = Path.GetFileName(dataFileName.LocalPath);
            var parent = parentNodeModel.Node;

            if (_projectService.CanAddResource(parent, dfName, false).HasFailed)
            {
                var dialogModel = new NameResourceViewModel("Add Data File", dfName, name => _projectService.CanAddResource(parent, name, false));
                if (await _interactions.RequestAsync(dialogModel) is not { } acceptedName)
                    return;

                dfName = acceptedName;
            }

            if (!await CloseStandaloneFileAsync(dataFileName.LocalPath))
                return;

            var df = new FileDataSource(dfName, dataFileName.LocalPath);
            var result = await _projectService.AddResourceAsync(parentNodeModel.Node, df);

            await result.Match(
                success =>
                {
                    SelectedNode = FindViewModel(success.Result);
                    return Task.CompletedTask;
                },
                async fail =>
                {
                    await _interactions.AlertAsync("Resource Error", fail.Reason);
                });
        }
    }

    [RelayCommand]
    public async Task AddNewPalette(ResourceNodeViewModel parentNodeModel)
    {
        var dialogModel = new AddPaletteViewModel(name => _projectService.CanAddResource(parentNodeModel.Node, name, false),
            _preferencesStore.Preferences.AddPalette);

        var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);
        var dataFiles = projectTree.EnumerateDepthFirst().Select(x => x.Item).OfType<FileDataSource>();
        dialogModel.DataSources = new(dataFiles);
        dialogModel.SelectedDataSource = dialogModel.DataSources.FirstOrDefault();
        dialogModel.TemplatePalettes = new([null, .. _paletteStore.GlobalPalettes.OrderBy(x => x.Name)]);

        if (dialogModel.DataSources.Count == 0)
        {
            await _interactions.AlertAsync("Project Error", "Project does not contain any data files to define a palette");
            return;
        }

        var dialogResult = await _interactions.RequestAsync(dialogModel);

        if (dialogResult is not null && dialogModel.SelectedDataSource is not null)
        {
            var template = dialogModel.TemplatePalette;
            var colorModel = template?.ColorModel ?? Palette.StringToColorModel(dialogModel.SelectedColorModel);
            IColorSource[] sources = template is null
                ? []
                : Enumerable.Range(0, template.Entries).Select(i => new ProjectNativeColorSource(template.GetNativeColor(i))).ToArray();

            var pal = new Palette(dialogModel.PaletteName, _colorFactory, colorModel, sources,
                dialogModel.ZeroIndexTransparent, PaletteStorageSource.ProjectXml, dialogModel.SelectedDataSource);

            var result = await _projectService.AddResourceAsync(parentNodeModel.Node, pal);

            await result.Match(
                async success =>
                {
                    SelectedNode = FindViewModel(success.Result);
                    _preferencesStore.Preferences.AddPalette = dialogModel.ToPreferences();
                    _preferencesStore.Save();
                    await _editors.ActivateEditor(pal);
                },
                async fail =>
                {
                    await _interactions.AlertAsync("Resource Error", fail.Reason);
                });
        }
    }

    [RelayCommand]
    public async Task AddNewScatteredArranger(ResourceNodeViewModel parentNodeModel)
    {
        var dialogModel = new AddScatteredArrangerViewModel(name => _projectService.CanAddResource(parentNodeModel.Node, name, false),
            _preferencesStore.Preferences.AddArranger);

        var dialogResult = await _interactions.RequestAsync(dialogModel);

        if (dialogResult is not null)
        {
            var arranger = dialogModel.SelectedLayout.Value switch
            {
                ElementLayout.Tiled => new ScatteredArranger(dialogModel.ArrangerName,
                    dialogModel.SelectedColorType.Value, dialogModel.SelectedLayout.Value,
                    dialogModel.TiledArrangerElementWidth, dialogModel.TiledArrangerElementHeight,
                    dialogModel.TiledElementPixelWidth, dialogModel.TiledElementPixelHeight),
                ElementLayout.Single => new ScatteredArranger(dialogModel.ArrangerName,
                    dialogModel.SelectedColorType.Value, dialogModel.SelectedLayout.Value,
                    1, 1,
                    dialogModel.SingleArrangerPixelWidth, dialogModel.SingleArrangerPixelHeight),
                _ => throw new InvalidOperationException($"Invalid layout: {dialogModel.SelectedLayout}")
            };

            var result = await _projectService.AddResourceAsync(parentNodeModel.Node, arranger);

            await result.Match(
                async success =>
                {
                    SelectedNode = FindViewModel(success.Result);
                    _preferencesStore.Preferences.AddArranger = dialogModel.ToPreferences();
                    _preferencesStore.Save();
                    await _editors.ActivateEditor(arranger);
                },
                async fail =>
                {
                    await _interactions.AlertAsync("Resource Error", fail.Reason);
                });
        }
    }

    [RelayCommand]
    public async Task ExportArrangerNodeAs(ResourceNodeViewModel nodeModel)
    {
        if (nodeModel is ArrangerNodeViewModel arrNodeModel && arrNodeModel.Node.Item is ScatteredArranger arranger)
        {
            await ExportArrangerAs(arranger);
        }
    }

    [RelayCommand]
    public async Task ExportArrangerAs(ScatteredArranger arranger)
    {
        if (await _editors.AlertIfMissingDataSourceAsync(arranger, "Export"))
            return;

        if (!await ResolveUnsavedChangesAsync(arranger, "exporting", discardOnNo: false))
            return;

        var exportFileName = await _fileSelect.RequestExportArrangerFileName($"{arranger.Name}.png");

        if (exportFileName is null)
            return;

        try
        {
            if (arranger.ColorType == PixelColorType.Indexed)
            {
                var image = new IndexedImage(arranger);
                image.ExportImage(exportFileName.LocalPath, new ImageSharpFileAdapter());
            }
            else if (arranger.ColorType == PixelColorType.Direct)
            {
                var image = new DirectImage(arranger);
                image.ExportImage(exportFileName.LocalPath, new ImageSharpFileAdapter());
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not export '{ArrangerName}' to '{FileName}'", arranger.Name, exportFileName.LocalPath);
            await _interactions.AlertAsync("Export Error", ex.Message);
        }
    }

    [RelayCommand]
    public async Task ImportArrangerNodeFrom(ResourceNodeViewModel nodeModel)
    {
        if (nodeModel is ArrangerNodeViewModel arrNodeModel && arrNodeModel.Node.Item is ScatteredArranger arranger)
        {
            await ImportArrangerFrom(arranger);
        }
    }

    [RelayCommand]
    public Task ImportArrangerFrom(ScatteredArranger arranger) => ImportArrangerInto(arranger, null);

    /// <param name="bounds">Arranger pixels the import may change, or null for the whole arranger</param>
    public async Task ImportArrangerInto(ScatteredArranger arranger, Rectangle? bounds)
    {
        if (arranger.GetReadOnlyReason() is { } reason)
        {
            await _interactions.AlertAsync("Import", $"'{arranger.Name}' is read-only because it {reason}");
            return;
        }

        if (await _editors.AlertIfMissingDataSourceAsync(arranger, "Import"))
            return;

        if (!await ResolveUnsavedChangesAsync(arranger, "importing", discardOnNo: true))
            return;

        var fileName = await _fileSelect.RequestImportArrangerFileName();

        if (fileName is null)
            return;

        var dialogModel = new ImportImageViewModel(arranger, fileName.LocalPath, _fileSelect, _preferencesStore, _interactions, bounds);
        await _interactions.RequestAsync(dialogModel);
    }

    /// <summary>
    /// Has the user resolve an open editor's unsaved changes so the operation works against the saved data
    /// </summary>
    /// <param name="verb">The operation, as in "Save them before {verb}?"</param>
    /// <param name="discardOnNo">Whether No discards the editor's changes; otherwise No keeps them and proceeds</param>
    /// <returns>False if the user cancelled or the save failed</returns>
    private async Task<bool> ResolveUnsavedChangesAsync(Arranger arranger, string verb, bool discardOnNo)
    {
        var editor = _editors.Editors.FirstOrDefault(x => ReferenceEquals(x.Resource, arranger));

        if (editor is not { IsModified: true })
            return true;

        var result = await _interactions.PromptAsync(PromptChoices.YesNoCancel, "Save Changes",
            $"'{editor.DisplayName}' has unsaved changes. Save them before {verb}?");

        if (result == PromptResult.Accept)
        {
            await editor.SaveChangesAsync();
            return !editor.IsModified;
        }

        if (result == PromptResult.Reject)
        {
            if (discardOnNo)
                editor.DiscardChanges();
            return true;
        }

        return false;
    }

    [RelayCommand]
    public async Task RequestRemoveNode(ResourceNodeViewModel nodeModel)
    {
        var deleteNode = nodeModel.Node;
        var previewResult = _projectService.PreviewResourceDeletion(deleteNode);
        if (previewResult.HasFailed)
        {
            await _interactions.AlertAsync("Delete", previewResult.AsError.Reason);
            return;
        }

        var plan = previewResult.AsSuccess.Result;

        var changeVm = new ResourceRemovalChangesViewModel(new ResourceChangeViewModel(deleteNode, plan.Tree.CreatePathKey(deleteNode),
            true, false, false), plan.Changes.Select(x => new ResourceChangeViewModel(x)).ToList());

        if (await _interactions.RequestAsync(changeVm) is not true)
            return;

        if (!await _editors.ConfirmRemovalAsync(plan))
            return;

        var deletionResult = await _projectService.ApplyResourceDeletionAsync(plan, _paletteStore.DefaultPalette);
        if (deletionResult.HasFailed)
            await _interactions.AlertAsync("Delete", deletionResult.AsError.Reason);
    }

    [RelayCommand]
    public async Task RenameNode(ResourceNodeViewModel nodeModel)
    {
        var dialogModel = new NameResourceViewModel($"Rename {nodeModel.Name}", nodeModel.Name,
            name => _projectService.CanRenameResource(nodeModel.Node, name));

        if (await _interactions.RequestAsync(dialogModel) is { } newName)
        {
            var result = await _projectService.RenameResourceAsync(nodeModel.Node, newName);
            if (result.HasFailed)
                await _interactions.AlertAsync("Rename failed", result.AsError.Reason);
        }
    }

    [RelayCommand]
    public async Task MoveNode(ResourceNodeViewModel nodeModel)
    {
        var node = nodeModel.Node;
        var tree = _projectService.GetContainingProject(node);

        var destinations = tree.EnumerateDepthFirst()
            .Where(x => x.Item is ResourceFolder)
            .Prepend(tree.Root)
            .Where(x => _projectService.CanMoveNode(node, x).HasSucceeded)
            .Select(x => new MoveDestinationModel(x, ReferenceEquals(x, tree.Root) ? tree.Root.Name : tree.CreatePathKey(x)))
            .ToList();

        if (destinations.Count == 0)
        {
            await _interactions.AlertAsync("Move", $"There are no folders that '{node.Name}' can be moved to");
            return;
        }

        if (await _interactions.RequestAsync(new MoveNodeViewModel(node.Name, destinations)) is not { } destination)
            return;

        await MoveNodeToAsync(node, destination);
    }

    public async Task MoveNodeToAsync(ResourceNode node, ResourceNode destination)
    {
        var result = await _projectService.MoveNodeAsync(node, destination);
        if (result.HasFailed)
        {
            await _interactions.AlertAsync("Move failed", result.AsError.Reason);
            return;
        }

        if (FindViewModel(destination) is { } destinationModel)
            destinationModel.IsExpanded = true;
        SelectedNode = FindViewModel(node);
    }

    public bool CanDropNode(ResourceNodeViewModel source, ResourceNodeViewModel target) =>
        _projectService.CanMoveNode(source.Node, target.Node).HasSucceeded;

    [RelayCommand]
    public async Task RelinkDataFile(DataFileNodeViewModel nodeModel)
    {
        if (nodeModel.Node.Item is not FileDataSource { IsMissing: true } dataSource)
        {
            nodeModel.IsMissing = false;
            return;
        }

        var fileName = await _fileSelect.RequestExistingDataFileName();
        if (fileName is null)
            return;

        var result = await _projectService.RelinkDataFileAsync(dataSource, fileName.LocalPath);
        if (result.HasFailed)
        {
            await _interactions.AlertAsync("Relink failed", result.AsError.Reason);
            return;
        }

        nodeModel.IsMissing = false;
    }

    public async void ReceiveAsync(AddScatteredArrangerFromCopyMessage message)
    {
        var copy = message.Copy;
        var projectTree = _projectService.GetContainingProject(message.ProjectResource);
        var dialogModel = new NameResourceViewModel("Name Resource", "", name => _projectService.CanAddResource(projectTree.Root, name, false));

        var dialogResult = await _interactions.RequestAsync(dialogModel);

        if (dialogResult is string resourceName)
        {
            var newArranger = new ScatteredArranger(resourceName, copy.ColorType, copy.Layout, copy.Width, copy.Height, copy.ElementPixelWidth, copy.ElementPixelHeight);
            var source = new Point(0, 0);
            var dest = new Point(0, 0);

            var copyResult = ElementCopier.CopyElements(copy, newArranger, source, dest, copy.Width, copy.Height);

            await copyResult.Match(
                async copySuccess =>
                {
                    var addResult = await _projectService.AddResourceAsync(projectTree.Root, newArranger);

                    await addResult.Match(
                        async addSuccess =>
                        {
                            SelectedNode = FindViewModel(addSuccess.Result);
                            await _editors.ActivateEditor(newArranger);
                        },
                        async addFailed => await _interactions.AlertAsync("Error", addFailed.Reason)
                    );
                },
                async copyFailed => await _interactions.AlertAsync("Error", copyFailed.Reason)
            );
        }
    }

    [RelayCommand]
    public async Task AddNewProject()
    {
        var projectFileName = await _fileSelect.RequestNewProjectFileName();

        try
        {
            if (projectFileName is not null)
            {
                var result = await _projectService.CreateNewProjectAsync(Path.GetFullPath(projectFileName.LocalPath));
                if (result.HasFailed)
                    await _interactions.AlertAsync("Project Error", result.AsError.Reason);
            }
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync("Failed", $"Unable to create new project at location '{projectFileName}'\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    [RelayCommand]
    public async Task NewProjectFromFile()
    {
        var dataFileName = await _fileSelect.RequestExistingDataFileName();
        if (dataFileName is null)
            return;

        if (FindStandaloneFile(dataFileName.LocalPath) is { } standaloneVm)
        {
            await CreateProjectFromFile(standaloneVm);
            return;
        }

        if (FindDataFileViewModel(Projects, dataFileName.LocalPath) is { } projectDataVm)
        {
            RevealNode(projectDataVm);
            var holder = _projectService.GetContainingProject(projectDataVm.Node);
            await _interactions.AlertAsync("Project Error", $"'{Path.GetFileName(dataFileName.LocalPath)}' is already in project '{holder.Name}'");
            return;
        }

        if (await CreateProjectFromDataFileAsync(dataFileName.LocalPath) is { } tree)
            SelectedNode = FindViewModel(tree.Root);
    }

    [RelayCommand]
    public async Task CreateProjectFromFile(StandaloneFileNodeViewModel nodeModel)
    {
        var dataFileName = ((FileDataSource)nodeModel.Node.Item).FileLocation;

        if (!await CloseProject(nodeModel))
            return;

        if (await CreateProjectFromDataFileAsync(dataFileName) is { } tree)
            SelectedNode = FindViewModel(tree.Root.ChildNodes.First());
        else
            _projectService.OpenDataFile(dataFileName);
    }

    private StandaloneFileNodeViewModel? FindStandaloneFile(string fileName) =>
        FindDataFileViewModel(Projects.OfType<StandaloneFileNodeViewModel>(), fileName) as StandaloneFileNodeViewModel;

    private static ResourceNodeViewModel? FindDataFileViewModel(IEnumerable<ResourceNodeViewModel> roots, string fileName)
    {
        var path = Path.GetFullPath(fileName);
        return roots.SelectMany(x => x.SelfAndDescendants())
            .FirstOrDefault(x => x.Node.Item is FileDataSource source &&
                string.Equals(Path.GetFullPath(source.FileLocation), path, StringComparison.OrdinalIgnoreCase));
    }

    private void RevealNode(ResourceNodeViewModel nodeModel)
    {
        foreach (var ancestor in nodeModel.Ancestors())
            ancestor.IsExpanded = true;

        SelectedNode = nodeModel;
    }

    /// <summary>
    /// Closes the standalone tree for the file, if open, because its stream would block a second source on the same file
    /// </summary>
    /// <returns>False if the user cancelled closing it</returns>
    private async Task<bool> CloseStandaloneFileAsync(string fileName) =>
        FindStandaloneFile(fileName) is not { } standaloneVm || await CloseProject(standaloneVm);

    private async Task<ProjectTree?> CreateProjectFromDataFileAsync(string dataFileName)
    {
        var projectPath = Path.GetDirectoryName(dataFileName);
        if (projectPath is null)
        {
            await _interactions.AlertAsync("Directory Error", $"Could not get the directory name for {dataFileName}");
            return null;
        }

        var projectFileName = Path.Combine(projectPath, Path.GetFileNameWithoutExtension(dataFileName) + "Project.xml");

        try
        {
            var result = await _projectService.CreateNewProjectWithExistingFileAsync(Path.GetFullPath(projectFileName), Path.GetFullPath(dataFileName));
            if (result.HasSucceeded)
                return result.AsSuccess.Result;

            await _interactions.AlertAsync("Project Error", result.AsError.Reason);
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync("Failed", $"Unable to create new project at location '{projectFileName}'\n{ex.Message}\n{ex.StackTrace}");
        }

        return null;
    }

    public async Task OpenDataFile()
    {
        var dataFileName = await _fileSelect.RequestExistingDataFileName();
        if (dataFileName is null)
            return;

        var path = Path.GetFullPath(dataFileName.LocalPath);

        if (FindDataFileViewModel(Projects, path) is { } openVm)
        {
            RevealNode(openVm);
            return;
        }

        var result = _projectService.OpenDataFile(path);
        if (result.HasSucceeded)
            SelectedNode = FindViewModel(result.AsSuccess.Result.Root);
        else
            await _interactions.AlertAsync("Open File Error", result.AsError.Reason);
    }

    public async Task<bool> OpenProject()
    {
        var projectFileName = await _fileSelect.RequestProjectFileName();

        if (projectFileName is null)
            return false;

        return await OpenProject(projectFileName.LocalPath);
    }

    public async Task<bool> OpenProject(string projectFileName)
    {
        if (projectFileName is null)
            return false;

        var openResult = await _projectService.OpenProjectFileAsync(projectFileName);

        return await openResult.Match(
            async success =>
            {
                var dataFiles = success.Result.EnumerateDepthFirst().Select(x => x.Item).OfType<FileDataSource>().ToList();
                foreach (var dataFile in dataFiles)
                {
                    if (!await CloseStandaloneFileAsync(dataFile.FileLocation))
                    {
                        _projectService.CloseProject(success.Result);
                        return false;
                    }
                }

                await AlertMissingDataFiles(success.Result);
                return true;
            },
            async fail =>
            {
                var message = $"Project '{projectFileName}' contained {fail.Reasons.Count} errors{Environment.NewLine}" +
                    string.Join(Environment.NewLine, fail.Reasons);
                await _interactions.AlertAsync("Project Open Error", message);
                return false;
            });
    }

    private async Task AlertMissingDataFiles(ProjectTree tree)
    {
        var missing = tree.EnumerateDepthFirst()
            .Select(x => x.Item)
            .OfType<FileDataSource>()
            .Where(x => x.IsMissing)
            .Select(x => $"{x.Name} ({x.FileLocation})")
            .ToList();

        if (missing.Count == 0)
            return;

        await _interactions.AlertAsync("Missing Data Files",
            $"Project '{tree.Name}' references data files that could not be found:{Environment.NewLine}" +
            string.Join(Environment.NewLine, missing) + Environment.NewLine + Environment.NewLine +
            "Resources that read from them are unavailable until you right-click each file in the project tree and choose Relink...");
    }

    [RelayCommand]
    public async Task SaveProjectAs(ProjectNodeViewModel projectVm)
    {
        var projectTree = _projectService.GetContainingProject(projectVm.Node);

        var newFileName = await _fileSelect.RequestNewProjectFileName();

        if (newFileName is null)
            return;

        var saveAsResult = await _projectService.SaveProjectAsAsync(projectTree, newFileName.LocalPath);
        await saveAsResult.Match(
            success =>
            {
                return Task.CompletedTask;
            },
            async fail =>
            {
                await _interactions.AlertAsync("Project Save Error", fail.Reason);
            });
    }

    [RelayCommand]
    public async Task<bool> CloseProject(ResourceNodeViewModel projectVm)
    {
        var projectTree = _projectService.GetContainingProject(projectVm.Node);

        var projectSaveResult = await _projectService.SaveProjectAsync(projectTree);

        if (projectSaveResult.HasSucceeded)
        {
            var removedEditors = _editors.Editors
                .Where(x => projectTree.ContainsResource(x.Resource) || projectTree.ContainsResource(x.OriginatingProjectResource))
                .ToHashSet();

            foreach (var editor in removedEditors)
            {
                if (await _editors.RequestSaveUserChanges(editor, false) == UserSaveAction.Cancel)
                    return false;
            }

            _editors.RemoveEditors(removedEditors);

            var finalSaveResult = await _projectService.SaveProjectAsync(projectTree);
            if (finalSaveResult.HasFailed)
                await _interactions.AlertAsync("Project Save Error", $"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}: {finalSaveResult.AsError.Reason}");

            _projectService.CloseProject(projectTree);
            return true;
        }
        else if (projectSaveResult.HasFailed)
        {
            await _interactions.AlertAsync("Project Save Error", projectSaveResult.AsError.Reason);
            return false;
        }

        return false;
    }

    [RelayCommand]
    public async Task CloseAllProjects()
    {
        while (Projects.Count > 0)
        {
            var result = await CloseProject(Projects.First());
            if (result is false)
                return;
        }
    }

    [RelayCommand]
    public void ExploreResource(ResourceNodeViewModel nodeVm)
    {
        if (nodeVm.Node.DiskLocation is not null)
            _diskExploreService.ExploreDiskLocation(nodeVm.Node.DiskLocation);
    }

}
