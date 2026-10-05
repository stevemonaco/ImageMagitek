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
        _projectService.ProjectOpened += OnProjectOpened;
        _projectService.ProjectClosed += OnProjectClosed;
    }

    public bool HasProject => Projects.Any();

    private void OnProjectOpened(object? sender, ProjectTree tree)
    {
        Projects.Add(new ProjectNodeViewModel(tree));
        OnPropertyChanged(nameof(HasProject));
    }

    private void OnProjectClosed(object? sender, ProjectTree tree)
    {
        Projects.Remove(Projects.First(x => ReferenceEquals(x.Node, tree.Root)));
        OnPropertyChanged(nameof(HasProject));
    }

    private ResourceNodeViewModel? FindViewModel(ResourceNode node) =>
        Projects.Select(x => x.Find(node)).FirstOrDefault(x => x is not null);

    [ObservableProperty] private ObservableCollection<ProjectNodeViewModel> _projects = new();
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
        var result = _projectService.CreateNewFolder(parentNodeModel.Node, "New Folder");

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

    [RelayCommand]
    public async Task AddNewDataFile(ResourceNodeViewModel parentNodeModel)
    {
        var dataFileName = await _fileSelect.RequestExistingDataFileName();

        if (dataFileName is not null)
        {
            var dfName = Path.GetFileName(dataFileName.LocalPath);
            var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);

            if (parentNodeModel.Children.Any(x => x.Name == dfName))
            {
                await _interactions.AlertAsync("Error", $"'{parentNodeModel.Name}' already contains a resource named '{dfName}'");
                return;
            }

            var df = new FileDataSource(dfName, dataFileName.LocalPath);
            var result = _projectService.AddResource(parentNodeModel.Node, df);

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
        var dialogModel = new AddPaletteViewModel(parentNodeModel.Children.Select(x => x.Name), _preferencesStore.Preferences.AddPalette);

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

            var result = _projectService.AddResource(parentNodeModel.Node, pal);

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
        var dialogModel = new AddScatteredArrangerViewModel(parentNodeModel.Children.Select(x => x.Name), _preferencesStore.Preferences.AddArranger);
        var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);

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

            var result = _projectService.AddResource(parentNodeModel.Node, arranger);

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
        var exportFileName = await _fileSelect.RequestExportArrangerFileName($"{arranger.Name}.png");

        if (exportFileName is not null)
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
    public async Task ImportArrangerFrom(ScatteredArranger arranger)
    {
        if (arranger.IsReadOnly())
        {
            await _interactions.AlertAsync("Import", $"'{arranger.Name}' is read-only because it uses a codec that cannot encode");
            return;
        }

        if (!await ResolveUnsavedChangesBeforeImport(arranger))
            return;

        var fileName = await _fileSelect.RequestImportArrangerFileName();

        if (fileName is null)
            return;

        var dialogModel = new ImportImageViewModel(arranger, fileName.LocalPath, _fileSelect, _preferencesStore);
        await _interactions.RequestAsync(dialogModel);
    }

    /// <summary>
    /// Has the user save or discard an open editor's unsaved changes so the import previews and writes against the saved data
    /// </summary>
    /// <returns>False if the user cancelled or the save failed</returns>
    private async Task<bool> ResolveUnsavedChangesBeforeImport(Arranger arranger)
    {
        var editor = _editors.Editors.FirstOrDefault(x => ReferenceEquals(x.Resource, arranger));

        if (editor is not { IsModified: true })
            return true;

        var result = await _interactions.PromptAsync(PromptChoices.YesNoCancel, "Save Changes",
            $"'{editor.DisplayName}' has unsaved changes. Save them before importing?");

        if (result == PromptResult.Accept)
        {
            await editor.SaveChangesAsync();
            return !editor.IsModified;
        }

        if (result == PromptResult.Reject)
        {
            editor.DiscardChanges();
            return true;
        }

        return false;
    }

    [RelayCommand]
    public async Task RequestRemoveNode(ResourceNodeViewModel nodeModel)
    {
        var deleteNode = nodeModel.Node;
        var plan = _projectService.PreviewResourceDeletion(deleteNode);

        var changeVm = new ResourceRemovalChangesViewModel(new ResourceChangeViewModel(deleteNode, plan.Tree.CreatePathKey(deleteNode),
            true, false, false), plan.Changes.Select(x => new ResourceChangeViewModel(x)).ToList());

        if (await _interactions.RequestAsync(changeVm) is not true)
            return;

        if (!await _editors.ConfirmRemovalAsync(plan))
            return;

        var deletionResult = _projectService.ApplyResourceDeletion(plan, _paletteStore.DefaultPalette);
        if (deletionResult.HasFailed)
            await _interactions.AlertAsync("Delete", deletionResult.AsError.Reason);
    }

    [RelayCommand]
    public async Task RenameNode(ResourceNodeViewModel nodeModel)
    {
        var dialogModel = new RenameNodeViewModel(nodeModel);
        var dialogResult = await _interactions.RequestAsync(dialogModel);

        if (dialogResult is not null)
        {
            var result = await _projectService.RenameResourceAsync(nodeModel.Node, dialogModel.Name);
            if (result.HasFailed)
                await _interactions.AlertAsync("Rename failed", result.AsError.Reason);
        }
    }

    public async void ReceiveAsync(AddScatteredArrangerFromCopyMessage message)
    {
        var dialogModel = new NameResourceViewModel();
        var copy = message.Copy;
        var projectTree = _projectService.GetContainingProject(message.ProjectResource);

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
                    var addResult = _projectService.AddResource(projectTree.Root, newArranger);

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

    //public void DragOver(IDropInfo dropInfo)
    //{
    //    if (dropInfo.Data is ResourceNodeViewModel sourceModel && dropInfo.TargetItem is ResourceNodeViewModel targetModel)
    //    {
    //        _projectService.CanMoveNode(sourceModel.Node, targetModel.Node).Switch(
    //            success =>
    //            {
    //                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
    //                dropInfo.Effects = DragDropEffects.Move;
    //            },
    //            fail => { }
    //        );
    //    }
    //}

    //public void Drop(IDropInfo dropInfo)
    //{
    //    var targetModel = dropInfo.TargetItem as ResourceNodeViewModel;

    //    if (dropInfo.Data is ResourceNodeViewModel sourceModel && (targetModel is ResourceNodeViewModel || targetModel is FolderNodeViewModel))
    //    {
    //        var result = _projectService.MoveNode(sourceModel.Node, targetModel.Node);

    //        result.Switch(
    //            success =>
    //            {
    //                sourceModel.ParentModel.Children.Remove(sourceModel);
    //                sourceModel.ParentModel = targetModel;
    //                targetModel.Children.Add(sourceModel);
    //                SelectedNode = sourceModel;

    //                //_projectService.SaveProject(projectTree)
    //                //.Switch(
    //                //    success => IsModified = false,
    //                //    fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}: {fail.Reason}")
    //                //);
    //            },
    //            fail => _windowManager.ShowMessageBox($"{fail.Reason}", "Move Resource Error")
    //            );
    //    }
    //}

    [RelayCommand]
    public async Task AddNewProject()
    {
        var projectFileName = await _fileSelect.RequestNewProjectFileName();

        try
        {
            if (projectFileName is not null)
            {
                var result = _projectService.CreateNewProject(Path.GetFullPath(projectFileName.LocalPath));
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

        var projectPath = Path.GetDirectoryName(dataFileName.LocalPath);
        if (projectPath is null)
        {
            await _interactions.AlertAsync("Directory Error", $"Could not get the directory name for {dataFileName.LocalPath}");
            return;
        }

        var projectFileName = Path.Combine(projectPath, Path.GetFileNameWithoutExtension(dataFileName.LocalPath) + "Project.xml");

        try
        {
            var result = await _projectService.CreateNewProjectWithExistingFileAsync(Path.GetFullPath(projectFileName), Path.GetFullPath(dataFileName.LocalPath));
            await result.Match(
                success =>
                {
                    SelectedNode = FindViewModel(success.Result.Root);
                    return Task.CompletedTask;
                },
                async fail => await _interactions.AlertAsync("Project Error", $"{fail.Reason}"));
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync("Failed", $"Unable to create new project at location '{projectFileName}'\n{ex.Message}\n{ex.StackTrace}");
        }
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
            success => Task.FromResult(true),
            async fail =>
            {
                var message = $"Project '{projectFileName}' contained {fail.Reasons.Count} errors{Environment.NewLine}" +
                    string.Join(Environment.NewLine, fail.Reasons);
                await _interactions.AlertAsync("Project Open Error", message);
                return false;
            });
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
    public async Task<bool> CloseProject(ProjectNodeViewModel projectVm)
    {
        var projectTree = _projectService.GetContainingProject(projectVm.Node);

        var projectSaveResult = await _projectService.SaveProjectAsync(projectTree);

        if (projectSaveResult.HasSucceeded)
        {
            var activeContainedEditors = _editors.Editors.Where(x => projectTree.ContainsResource(x.Resource));
            // var activeSequentialEditors = _editors.Editors
            //     .OfType<SequentialArrangerEditorViewModel>()
            //     .Where(x => projectTree.ContainsResource(((SequentialArranger)x.Resource).ActiveDataSource));
            // var activeIndexedPixelEditors = _editors.Editors
            //     .OfType<IndexedPixelEditorViewModel>()
            //     .Where(x => projectTree.ContainsResource(x.OriginatingProjectResource));
            // var activeDirectPixelEditors = _editors.Editors
            //     .OfType<DirectPixelEditorViewModel>()
            //     .Where(x => projectTree.ContainsResource(x.OriginatingProjectResource));

            var removedEditors = new HashSet<ResourceEditorBaseViewModel>(activeContainedEditors);
            // removedEditors.UnionWith(activeSequentialEditors);
            // removedEditors.UnionWith(activeIndexedPixelEditors);
            // removedEditors.UnionWith(activeDirectPixelEditors);

            foreach (var editor in removedEditors)
            {
                if (await _editors.RequestSaveUserChanges(editor, false) == UserSaveAction.Cancel)
                    return false;
            }

            foreach (var editor in removedEditors)
            {
                _editors.Editors.Remove(editor);
            }

            _editors.ActiveEditor = _editors.Editors.FirstOrDefault();

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