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
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Services;
using Jot;
using Monaco.PathTree;
using TileShop.Shared.EventModels;
using TileShop.Shared.Models;
using TileShop.Shared.Services;
using TileShop.Shared.Interactions;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class ProjectTreeViewModel : ToolViewModel
{
    private readonly IProjectService _projectService;
    private readonly IPaletteService _paletteService;
    private readonly IAsyncFileRequestService _fileSelect;
    private readonly IInteractionService _interactions;
    private readonly Tracker _tracker;
    private readonly IExploreService _diskExploreService;
    private readonly EditorsViewModel _editors;

    public ProjectTreeViewModel(IProjectService solutionService, IPaletteService paletteService,
        IAsyncFileRequestService fileSelect, IInteractionService interactionService,
        Tracker tracker, IExploreService diskExploreService, EditorsViewModel editors)
    {
        _projectService = solutionService;
        _paletteService = paletteService;
        _fileSelect = fileSelect;
        _interactions = interactionService;
        _tracker = tracker;
        _diskExploreService = diskExploreService;
        _editors = editors;

        Messenger.Register<AddScatteredArrangerFromCopyEvent>(this, (r, m) => ReceiveAsync(m));

        //DisplayName = "Project Tree";
    }

    public bool HasProject => Projects.Any();

    [ObservableProperty] private ObservableCollection<ProjectNodeViewModel> _projects = new();
    [ObservableProperty] private ResourceNodeViewModel? _selectedNode;

    [RelayCommand]
    public void ActivateSelectedNode()
    {
        if (SelectedNode is ProjectNodeViewModel || SelectedNode is FolderNodeViewModel)
        {
            SelectedNode.IsExpanded ^= true;
        }
        else if (SelectedNode?.Node?.Item is not null)
        {
            _editors.ActivateEditor(SelectedNode.Node.Item);
        }
    }

    [RelayCommand]
    public async Task AddNewFolder(ResourceNodeViewModel parentNodeModel)
    {
        var result = _projectService.CreateNewFolder(parentNodeModel.Node, "New Folder");

        await result.Match(
            (Func<MagitekResult<ResourceNode>.Success, Task>)(            success =>
            {
                var folderVM = new FolderNodeViewModel(success.Result, parentNodeModel);
                parentNodeModel.Children.Add((ResourceNodeViewModel)folderVM);
                this.SelectedNode = folderVM;
                base.IsModified = true;
                return Task.CompletedTask;
            }),
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
                (Func<MagitekResult<ResourceNode>.Success, Task>)(                success =>
                {
                    var dfVM = new DataFileNodeViewModel(success.Result, parentNodeModel);
                    parentNodeModel.Children.Add((ResourceNodeViewModel)dfVM);
                    this.SelectedNode = dfVM;
                    base.IsModified = true;
                    return Task.CompletedTask;
                }),
                async fail =>
                {
                    await _interactions.AlertAsync("Resource Error", fail.Reason);
                });
        }
    }

    [RelayCommand]
    public async Task AddNewPalette(ResourceNodeViewModel parentNodeModel)
    {
        var dialogModel = new AddPaletteViewModel(parentNodeModel.Children.Select(x => x.Name));

        var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);
        var dataFiles = projectTree.EnumerateDepthFirst().Select(x => x.Item).OfType<DataSource>();
        dialogModel.DataSources = new(dataFiles);
        dialogModel.SelectedDataSource = dialogModel.DataSources.FirstOrDefault();
        dialogModel.ColorModels = new(Palette.GetColorModelNames());

        _tracker.Track(dialogModel);

        if (dialogModel.DataSources.Count == 0)
        {
            await _interactions.AlertAsync("Project Error", "Project does not contain any data files to define a palette");
            return;
        }

        var dialogResult = await _interactions.RequestAsync(dialogModel);

        if (dialogResult is not null)
        {
            var pal = new Palette(dialogModel.PaletteName, _paletteService.ColorFactory,
                Palette.StringToColorModel(dialogModel.SelectedColorModel), Array.Empty<IColorSource>(),
                dialogModel.ZeroIndexTransparent, PaletteStorageSource.Project);

            pal.DataSource = dialogModel.SelectedDataSource;

            var result = _projectService.AddResource(parentNodeModel.Node, pal);

            await result.Match((Func<MagitekResult<ResourceNode>.Success, Task>)(success =>
                {
                    var palVM = new PaletteNodeViewModel(success.Result, parentNodeModel);
                    parentNodeModel.Children.Add((ResourceNodeViewModel)palVM);
                    this.SelectedNode = palVM;
                    base.IsModified = true;
                    _tracker.Persist(dialogModel);
                    _editors.ActivateEditor(pal);
                    return Task.CompletedTask;
                }),
                async fail =>
                {
                    await _interactions.AlertAsync("Resource Error", fail.Reason);
                });
        }
    }

    [RelayCommand]
    public async Task AddNewScatteredArranger(ResourceNodeViewModel parentNodeModel)
    {
        var dialogModel = new AddScatteredArrangerViewModel(parentNodeModel.Children.Select(x => x.Name));
        var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);
        _tracker.Track(dialogModel);

        var dialogResult = await _interactions.RequestAsync(dialogModel);

        if (dialogResult is not null)
        {
            var arranger = new ScatteredArranger(dialogModel.ArrangerName, dialogModel.ColorType,
                dialogModel.Layout, dialogModel.ArrangerElementWidth, dialogModel.ArrangerElementHeight,
                dialogModel.ElementPixelWidth, dialogModel.ElementPixelHeight);

            var result = _projectService.AddResource(parentNodeModel.Node, arranger);

            await result.Match(
                (Func<MagitekResult<ResourceNode>.Success, Task>)(                success =>
                {
                    var arrangerVM = new ArrangerNodeViewModel(success.Result, parentNodeModel);
                    parentNodeModel.Children.Add((ResourceNodeViewModel)arrangerVM);
                    this.SelectedNode = arrangerVM;
                    base.IsModified = true;
                    _tracker.Persist(dialogModel);
                    _editors.ActivateEditor(arranger);
                    return Task.CompletedTask;
                }),
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
        var dialogModel = new ImportImageViewModel(arranger, _fileSelect);
        var dialogResult = await _interactions.RequestAsync(dialogModel);

        if (dialogResult is not null)
        {
            var changeEvent = new ArrangerChangedEvent(arranger, ArrangerChange.Pixels);
            Messenger.Send(changeEvent);
        }
    }

    [RelayCommand]
    public async Task RequestRemoveNode(ResourceNodeViewModel nodeModel)
    {
        var deleteNode = nodeModel.Node;
        var tree = _projectService.GetContainingProject(nodeModel.Node);
        var changes = _projectService.PreviewResourceDeletionChanges(deleteNode).ToList();

        var changeVm = new ResourceRemovalChangesViewModel(new ResourceChangeViewModel(deleteNode, tree.CreatePathKey(deleteNode),
            true, false, false), changes.Select(x => new ResourceChangeViewModel(x)).ToList());

        var dialogResult = await _interactions.RequestAsync(changeVm);

        if (dialogResult is true)
        {
            var modifiedEditors = _editors.Editors.Where(x => x.IsModified);

            _editors.Editors.Clear();
            _editors.ActiveEditor = null;

            _projectService.ApplyResourceDeletionChanges(changes, _paletteService.DefaultPalette);
            var projectRootVm = Projects.First(x => ReferenceEquals(tree.Root, x.Node));
            SynchronizeTree(projectRootVm);

            _projectService.SaveProject(tree)
            .Switch(
                (Action<MagitekResult.Success>)(                success => base.IsModified = false),
                async fail => await _interactions.AlertAsync("Project Error", $"An error occurred while saving the project tree to {tree.Root.DiskLocation}: {fail.Reason}")
            );
        }
    }

    private void SynchronizeTree(ResourceNodeViewModel projectVm)
    {
        var vmStack = new Stack<ResourceNodeViewModel>();
        vmStack.Push(projectVm);

        while (vmStack.Count > 0)
        {
            var vmNode = vmStack.Pop();
            SynchronizeNode(vmNode.Node, vmNode);

            foreach (var child in vmNode.Children)
            {
                vmStack.Push(child);
            }
        }
    }

    private void SynchronizeNode(ResourceNode resourceNode, ResourceNodeViewModel vmNode)
    {
        if (resourceNode.ChildNodes is null)
            return;

        if (!resourceNode.ChildNodes.All(x => vmNode.Children.Any(y => ReferenceEquals(x, y.Node))) &&
            resourceNode.ChildNodes.Count() == vmNode.Children.Count)
            return;

        SynchronizeDeletions(resourceNode, vmNode);
        SynchronizeInsertions(resourceNode, vmNode);

        void SynchronizeDeletions(ResourceNode resourceNode, ResourceNodeViewModel vmNode)
        {
            List<ResourceNodeViewModel> removedItems = default;

            foreach (var vm in vmNode.Children)
            {
                if (!resourceNode.ChildNodes.Any(x => ReferenceEquals(x, vm.Node)))
                {
                    if (removedItems is null)
                        removedItems = new List<ResourceNodeViewModel>();

                    removedItems.Add(vm);
                }
            }

            if (removedItems is not null)
            {
                foreach (var vm in removedItems)
                    vmNode.Children.Remove(vm);
            }
        }

        void SynchronizeInsertions(ResourceNode resourceNode, ResourceNodeViewModel vmNode)
        {
            foreach (var node in resourceNode.ChildNodes)
            {
                if (!vmNode.Children.Any(x => ReferenceEquals(node, x.Node)))
                {
                    ResourceNodeViewModel newNode = node switch
                    {
                        ResourceFolderNode _ => new FolderNodeViewModel(node, vmNode),
                        PaletteNode _ => new PaletteNodeViewModel(node, vmNode),
                        DataFileNode _ => new DataFileNodeViewModel(node, vmNode),
                        ArrangerNode _ => new ArrangerNodeViewModel(node, vmNode),
                        ProjectNode _ => throw new InvalidOperationException($"{nameof(SynchronizeInsertions)}: Inserting a project node '{node.Name}' is not supported"),
                        _ => throw new InvalidOperationException($"{nameof(SynchronizeInsertions)}: Inserting a node '{node.Name}' of type '{node.GetType()}' is not supported")
                    };

                    vmNode.Children.Add(newNode);
                }
            }
        }
    }

    [RelayCommand]
    public async Task RenameNode(ResourceNodeViewModel nodeModel)
    {
        var dialogModel = new RenameNodeViewModel(nodeModel);
        var dialogResult = await _interactions.RequestAsync(dialogModel);

        if (dialogResult is string)
        {
            var oldName = nodeModel.Name;
            var newName = dialogModel.Name;

            _projectService.RenameResource(nodeModel.Node, (string?)dialogModel.Name).Switch(
                (Action<MagitekResult.Success>)(                success =>
                {
                    nodeModel.Name = newName;

                    if (nodeModel.ParentModel is FolderNodeViewModel || nodeModel.ParentModel is ProjectNodeViewModel)
                        nodeModel.ParentModel.NotifyChildrenChanged();

                    var renameEvent = new ResourceRenamedEvent(nodeModel.Node.Item, (string?)newName, (string)oldName);
                    Messenger.Send<ResourceRenamedEvent>(renameEvent);
                }),
                async fail => await _interactions.AlertAsync("Rename failed", fail.Reason));
        }
    }

    public async void ReceiveAsync(AddScatteredArrangerFromCopyEvent message)
    {
        var dialogModel = new NameResourceViewModel();
        var copy = message.Copy;
        var arranger = message.Copy.Source;
        var projectTree = _projectService.GetContainingProject(message.ProjectResource);
        var parentModel = Projects.First(x => ReferenceEquals(projectTree.Project, x.Node.Item));

        var dialogResult = await _interactions.RequestAsync(dialogModel);

        if (dialogResult is string resourceName)
        {
            var newArranger = new ScatteredArranger(resourceName, arranger.ColorType, arranger.Layout, copy.Width, copy.Height, copy.ElementPixelWidth, copy.ElementPixelHeight);
            var source = new Point(0, 0);
            var dest = new Point(0, 0);

            var copyResult = ElementCopier.CopyElements(copy, newArranger, source, dest, copy.Width, copy.Height);

            copyResult.Switch(
                (Action<MagitekResult.Success>)(                copySuccess =>
                {
                    var addResult = _projectService.AddResource((ResourceNode)parentModel.Node, newArranger);

                    addResult.Switch(
                        (Action<MagitekResult<ResourceNode>.Success>)(                        addSuccess =>
                        {
                            var arrangerVM = new ArrangerNodeViewModel(addSuccess.Result, (ResourceNodeViewModel)parentModel);
                            parentModel.Children.Add((ResourceNodeViewModel)arrangerVM);
                            this.SelectedNode = arrangerVM;
                            base.IsModified = true;
                            _editors.ActivateEditor(newArranger);
                        }),
                        async addFailed => await _interactions.AlertAsync("Error", addFailed.Reason)
                    );
                }),
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
    public override async Task SaveChangesAsync()
    {
        try
        {
            foreach (var project in Projects)
            {
                var projectTree = _projectService.GetContainingProject(project.Node);

                _projectService.SaveProject(projectTree)
                     .Switch(
                         (Action<MagitekResult.Success>)(                         success => base.IsModified = false),
                         async fail => await _interactions.AlertAsync("Project Error", $"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}: {fail.Reason}")
                     );
            }
        }
        catch (Exception ex)
        {
            await _interactions.AlertAsync("Project Error", $"Unable to save project:\n{ex.Message}\n{ex.StackTrace}");
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
                _projectService.CreateNewProject(Path.GetFullPath(projectFileName.LocalPath)).Switch(
                    success =>
                    {
                        var projectVM = new ProjectNodeViewModel((ProjectNode)success.Result.Root);
                        Projects.Add(projectVM);
                        OnPropertyChanged(nameof(HasProject));
                        Messenger.Send(new ProjectLoadedEvent(projectVM.Node.DiskLocation));
                    },
                    async fail => await _interactions.AlertAsync("Project Error", $"{fail.Reason}"));
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
        var projectFileName = Path.Combine(projectPath, Path.GetFileNameWithoutExtension(dataFileName.LocalPath) + "Project.xml");

        try
        {
            if (dataFileName is not null)
            {
                var result = _projectService.CreateNewProjectWithExistingFile(Path.GetFullPath(projectFileName), Path.GetFullPath(dataFileName.LocalPath));
                await result.Match(
                    success =>
                    {
                        var projectVM = new ProjectNodeViewModel((ProjectNode)success.Result.Root);
                        Projects.Add(projectVM);
                        SelectedNode = projectVM;
                        IsModified = true;

                        OnPropertyChanged(nameof(HasProject));
                        Messenger.Send(new ProjectLoadedEvent(projectVM.Node.DiskLocation));
                        return Task.CompletedTask;
                    },
                    async fail => await _interactions.AlertAsync("Project Error", $"{fail.Reason}"));
            }
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

        var openResult = _projectService.OpenProjectFile(projectFileName);

        return await openResult.Match(
            success =>
            {
                var projectVM = new ProjectNodeViewModel((ProjectNode)success.Result.Root);
                Projects.Add(projectVM);
                OnPropertyChanged(nameof(HasProject));
                Messenger.Send(new ProjectLoadedEvent(projectFileName));
                return Task.FromResult(true);
            },
            async fail =>
            {
                var message = $"Project '{projectFileName}' contained {fail.Reasons.Count} errors{Environment.NewLine}" +
                    string.Join(Environment.NewLine, fail.Reasons);
                await _interactions.AlertAsync("Project Open Error", message);
                return false;
            });
    }

    [RelayCommand]
    public async Task SaveProjectAs(ProjectNodeViewModel projectVM)
    {
        var projectTree = _projectService.GetContainingProject(projectVM.Node);

        var newFileName = await _fileSelect.RequestNewProjectFileName();

        if (newFileName is null)
            return;

        await _projectService.SaveProjectAs(projectTree, newFileName.LocalPath).Match(
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
    public async Task<bool> CloseProject(ProjectNodeViewModel projectVM)
    {
        var projectTree = _projectService.GetContainingProject(projectVM.Node);

        var projectSaveResult = _projectService.SaveProject(projectTree);

        if (projectSaveResult.HasSucceeded)
        {
            var activeContainedEditors = _editors.Editors.Where(x => projectTree.ContainsResource(x.Resource));
            var activeSequentialEditors = _editors.Editors
                .OfType<SequentialArrangerEditorViewModel>()
                .Where(x => projectTree.ContainsResource(((SequentialArranger)x.Resource).ActiveDataSource));
            var activeIndexedPixelEditors = _editors.Editors
                .OfType<IndexedPixelEditorViewModel>()
                .Where(x => projectTree.ContainsResource(x.OriginatingProjectResource));
            var activeDirectPixelEditors = _editors.Editors
                .OfType<DirectPixelEditorViewModel>()
                .Where(x => projectTree.ContainsResource(x.OriginatingProjectResource));

            var removedEditors = new HashSet<ResourceEditorBaseViewModel>(activeContainedEditors);
            removedEditors.UnionWith(activeSequentialEditors);
            removedEditors.UnionWith(activeIndexedPixelEditors);
            removedEditors.UnionWith(activeDirectPixelEditors);

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

            await _projectService.SaveProject(projectTree).Match(
                 (Func<MagitekResult.Success, Task>)(                 success =>
                 {
                     base.IsModified = false;
                     return Task.CompletedTask;
                 }),
                 async fail =>
                 {
                     await _interactions.AlertAsync("Project Save Error", $"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}: {fail.Reason}");
                 });

            _projectService.CloseProject(projectTree);
            Projects.Remove(projectVM);
            OnPropertyChanged(nameof(HasProject));
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
    public void ExploreResource(ResourceNodeViewModel nodeVM)
    {
        _diskExploreService.ExploreDiskLocation(nodeVM.Node.DiskLocation);
    }

    public override void DiscardChanges()
    {
    }
}