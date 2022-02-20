using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagitek;
using ImageMagitek.Project;
using ImageMagitek.Services;
using Jot;
using TileShop.AvaloniaUI.ViewExtenders;
using TileShop.Shared.EventModels;
using TileShop.Shared.Services;

namespace TileShop.AvaloniaUI.ViewModels;

public partial class ProjectTreeViewModel : ToolViewModel
{
    private readonly IProjectService _projectService;
    private readonly IPaletteService _paletteService;
    private readonly IFileSelectService _fileSelect;
    private readonly IWindowManager _windowManager;
    private readonly Tracker _tracker;
    private readonly IDiskExploreService _diskExploreService;
    private readonly EditorsViewModel _editors;

    public ProjectTreeViewModel(IProjectService solutionService, IPaletteService paletteService,
        IFileSelectService fileSelect, IWindowManager windowManager,
        Tracker tracker, IDiskExploreService diskExploreService, EditorsViewModel editors)
    {
        _projectService = solutionService;
        _paletteService = paletteService;
        _fileSelect = fileSelect;
        _windowManager = windowManager;
        _tracker = tracker;
        _diskExploreService = diskExploreService;
        _editors = editors;

        Messenger.Register<AddScatteredArrangerFromCopyEvent>(this, (r, m) => Handle(m));

        //DisplayName = "Project Tree";
    }

    public bool HasProject => Projects.Any();

    [ObservableProperty] private ObservableCollection<ProjectNodeViewModel> _projects = new();

    [ObservableProperty] private ResourceNodeViewModel? _selectedNode;

    private readonly Dictionary<MessageBoxResult, string> _messageBoxLabels = new Dictionary<MessageBoxResult, string>
    {
        { MessageBoxResult.Yes, "Save" }, { MessageBoxResult.No, "Discard" }, { MessageBoxResult.Cancel, "Cancel" }
    };

    [ICommand]
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

    [ICommand]
    public void AddNewFolder(ResourceNodeViewModel parentNodeModel)
    {
        _projectService.CreateNewFolder(parentNodeModel.Node, "New Folder").Switch(
            success =>
            {
                var folderVM = new FolderNodeViewModel(success.Result, parentNodeModel);
                parentNodeModel.Children.Add(folderVM);
                SelectedNode = folderVM;
                IsModified = true;
            },
            fail =>
            {
                _windowManager.ShowMessageBox(fail.Reason, "Folder Creation Error");
            });
    }

    [ICommand]
    public void AddNewDataFile(ResourceNodeViewModel parentNodeModel)
    {
        var dataFileName = _fileSelect.GetExistingDataFileNameByUser();

        if (dataFileName is not null)
        {
            var dfName = Path.GetFileName(dataFileName);
            var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);

            if (parentNodeModel.Children.Any(x => x.Name == dfName))
            {
                _windowManager.ShowMessageBox($"'{parentNodeModel.Name}' already contains a resource named '{dfName}'", "Error");
                return;
            }

            var df = new FileDataSource(dfName, dataFileName);
            var result = _projectService.AddResource(parentNodeModel.Node, df);

            result.Switch(success =>
            {
                var dfVM = new DataFileNodeViewModel(success.Result, parentNodeModel);
                parentNodeModel.Children.Add(dfVM);
                SelectedNode = dfVM;
                IsModified = true;
            },
            fail =>
            {
                _windowManager.ShowMessageBox(fail.Reason, "Resource Error");
            });
        }
    }

    [ICommand]
    public void AddNewPalette(ResourceNodeViewModel parentNodeModel)
    {
        //var dialogModel = new AddPaletteViewModel(parentNodeModel.Children.Select(x => x.Name));

        //var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);
        //var dataFiles = projectTree.EnumerateDepthFirst().Select(x => x.Item).OfType<DataSource>();
        //dialogModel.DataSources.AddRange(dataFiles);
        //dialogModel.SelectedDataSource = dialogModel.DataSources.FirstOrDefault();
        //dialogModel.ColorModels.AddRange(Palette.GetColorModelNames());

        //_tracker.Track(dialogModel);

        //if (dialogModel.DataSources.Count == 0)
        //{
        //    _windowManager.ShowMessageBox("Project does not contain any data files to define a palette", "Project Error");
        //    return;
        //}

        //if (_windowManager.ShowDialog(dialogModel) is true)
        //{
        //    var pal = new Palette(dialogModel.PaletteName, _paletteService.ColorFactory,
        //        Palette.StringToColorModel(dialogModel.SelectedColorModel), Array.Empty<IColorSource>(),
        //        dialogModel.ZeroIndexTransparent, PaletteStorageSource.Project);

        //    pal.DataSource = dialogModel.SelectedDataSource;

        //    var result = _projectService.AddResource(parentNodeModel.Node, pal);

        //    result.Switch(success =>
        //    {
        //        var palVM = new PaletteNodeViewModel(success.Result, parentNodeModel);
        //        parentNodeModel.Children.Add(palVM);
        //        SelectedNode = palVM;
        //        IsModified = true;
        //        _tracker.Persist(dialogModel);
        //        _editors.ActivateEditor(pal);
        //    },
        //    fail =>
        //    {
        //        _windowManager.ShowMessageBox(fail.Reason, "Resource Error");
        //    });
        //}
    }

    [ICommand]
    public void AddNewScatteredArranger(ResourceNodeViewModel parentNodeModel)
    {
        //var dialogModel = new AddScatteredArrangerViewModel(parentNodeModel.Children.Select(x => x.Name));
        //var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);
        //_tracker.Track(dialogModel);

        //if (_windowManager.ShowDialog(dialogModel) is true)
        //{
        //    var arranger = new ScatteredArranger(dialogModel.ArrangerName, dialogModel.ColorType,
        //        dialogModel.Layout, dialogModel.ArrangerElementWidth, dialogModel.ArrangerElementHeight,
        //        dialogModel.ElementPixelWidth, dialogModel.ElementPixelHeight);

        //    var result = _projectService.AddResource(parentNodeModel.Node, arranger);

        //    result.Switch(success =>
        //    {
        //        var arrangerVM = new ArrangerNodeViewModel(success.Result, parentNodeModel);
        //        parentNodeModel.Children.Add(arrangerVM);
        //        SelectedNode = arrangerVM;
        //        IsModified = true;
        //        _tracker.Persist(dialogModel);
        //        _editors.ActivateEditor(arranger);
        //    },
        //    fail =>
        //    {
        //        _windowManager.ShowMessageBox(fail.Reason, "Resource Error");
        //    });
        //}
    }

    [ICommand]
    public void ExportArrangerNodeAs(ResourceNodeViewModel nodeModel)
    {
        //if (nodeModel is ArrangerNodeViewModel arrNodeModel)
        //{
        //    var arranger = arrNodeModel.Node.Item as ScatteredArranger;
        //    ExportArrangerAs(arranger);
        //}
    }

    [ICommand]
    public void ExportArrangerAs(ScatteredArranger arranger)
    {
        //var exportFileName = _fileSelect.GetExportArrangerFileNameByUser($"{arranger.Name}.png");

        //if (exportFileName is not null)
        //{
        //    if (arranger.ColorType == PixelColorType.Indexed)
        //    {
        //        var image = new IndexedImage(arranger);
        //        image.ExportImage(exportFileName, new ImageSharpFileAdapter());
        //    }
        //    else if (arranger.ColorType == PixelColorType.Direct)
        //    {
        //        var image = new DirectImage(arranger);
        //        image.ExportImage(exportFileName, new ImageSharpFileAdapter());
        //    }
        //}
    }

    [ICommand]
    public void ImportArrangerNodeFrom(ResourceNodeViewModel nodeModel)
    {
        //if (nodeModel is ArrangerNodeViewModel arrNodeModel && arrNodeModel.Node.Item is ScatteredArranger arranger)
        //{
        //    ImportArrangerFrom(arranger);
        //}
    }

    [ICommand]
    public void ImportArrangerFrom(ScatteredArranger arranger)
    {
        //var model = new ImportImageViewModel(arranger, _fileSelect);
        //if (_windowManager.ShowDialog(model) is true)
        //{
        //    var changeEvent = new ArrangerChangedEvent(arranger, ArrangerChange.Pixels);
        //    _events.PublishOnUIThread(changeEvent);
        //}
    }

    [ICommand]
    public void RequestRemoveNode(ResourceNodeViewModel nodeModel)
    {
        //var deleteNode = nodeModel.Node;
        //var tree = _projectService.GetContainingProject(nodeModel.Node);
        //var changes = _projectService.PreviewResourceDeletionChanges(deleteNode).ToList();

        //var changeVm = new ResourceRemovalChangesViewModel(new ResourceChangeViewModel(deleteNode, tree.CreatePathKey(deleteNode),
        //    true, false, false), changes.Select(x => new ResourceChangeViewModel(x)).ToList());

        //bool? result;
        //result = _windowManager.ShowDialog(changeVm);

        //if (result is true)
        //{
        //    var modifiedEditors = _editors.Editors.Where(x => x.IsModified);

        //    if (modifiedEditors.Any())
        //    {
        //        var boxResult = _windowManager.ShowMessageBox("The project contains modified items which must be saved or discarded before removing any items", "Save changes",
        //            MessageBoxButton.YesNoCancel, buttonLabels: _messageBoxLabels);

        //        if (boxResult == MessageBoxResult.Yes)
        //        {
        //            foreach (var editor in modifiedEditors)
        //                editor.SaveChanges();
        //        }
        //        else if (boxResult == MessageBoxResult.No)
        //        {
        //            foreach (var editor in modifiedEditors)
        //                editor.DiscardChanges();
        //        }
        //        else if (boxResult == MessageBoxResult.Cancel)
        //            return;
        //    }

        //    _editors.Editors.Clear();
        //    _editors.ActiveEditor = null;

        //    _projectService.ApplyResourceDeletionChanges(changes, _paletteService.DefaultPalette);
        //    var projectRootVm = Projects.First(x => ReferenceEquals(tree.Root, x.Node));
        //    SynchronizeTree(projectRootVm);

        //    _projectService.SaveProject(tree)
        //    .Switch(
        //        success => IsModified = false,
        //        fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {tree.Root.DiskLocation}: {fail.Reason}")
        //    );
        //}
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

    [ICommand]
    public async void RenameNode(ResourceNodeViewModel nodeModel)
    {
        var dialogModel = new RenameNodeViewModel(nodeModel);
        var dialogResult = await _windowManager.ShowDialog<RenameNodeViewModel, string?>(dialogModel);

        if (dialogResult is string)
        {
            var oldName = nodeModel.Name;
            var newName = dialogModel.Name;

            _projectService.RenameResource(nodeModel.Node, dialogModel.Name).Switch(
                success =>
                {
                    nodeModel.Name = newName;

                    if (nodeModel.ParentModel is FolderNodeViewModel || nodeModel.ParentModel is ProjectNodeViewModel)
                        nodeModel.ParentModel.NotifyChildrenChanged();

                    var renameEvent = new ResourceRenamedEvent(nodeModel.Node.Item, newName, oldName);
                    Messenger.Send(renameEvent);
                },
                fail => { }); //_windowManager.ShowMessageBox(fail.Reason, icon: MessageBoxImage.Error));
        }
    }

    public void Handle(AddScatteredArrangerFromCopyEvent message)
    {
        //var model = new NameResourceViewModel();
        //var copy = message.Copy;
        //var arranger = message.Copy.Source;
        //var projectTree = _projectService.GetContainingProject(message.ProjectResource);
        //var parentModel = Projects.First(x => ReferenceEquals(projectTree.Project, x.Node.Item));

        //if (_windowManager.ShowDialog(model) is true)
        //{
        //    var newArranger = new ScatteredArranger(model.ResourceName, arranger.ColorType, arranger.Layout, copy.Width, copy.Height, copy.ElementPixelWidth, copy.ElementPixelHeight);
        //    var source = new Point(0, 0);
        //    var dest = new Point(0, 0);

        //    var copyResult = ElementCopier.CopyElements(copy, newArranger, source, dest, copy.Width, copy.Height);

        //    copyResult.Switch(
        //        copySuccess =>
        //        {
        //            var addResult = _projectService.AddResource(parentModel.Node, newArranger);

        //            addResult.Switch(
        //                addSuccess =>
        //                {
        //                    var arrangerVM = new ArrangerNodeViewModel(addSuccess.Result, parentModel);
        //                    parentModel.Children.Add(arrangerVM);
        //                    SelectedNode = arrangerVM;
        //                    IsModified = true;
        //                    _editors.ActivateEditor(newArranger);
        //                },
        //                addFailed => _windowManager.ShowMessageBox($"{addFailed.Reason}", "Error")
        //            );
        //        },
        //        copyFailed => _windowManager.ShowMessageBox($"{copyFailed.Reason}", "Error")
        //    );
        //}
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

    [ICommand]
    public override void SaveChanges()
    {
        try
        {
            foreach (var project in Projects)
            {
                var projectTree = _projectService.GetContainingProject(project.Node);

                _projectService.SaveProject(projectTree)
                     .Switch(
                         success => IsModified = false,
                         fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}: {fail.Reason}")
                     );
            }
        }
        catch (Exception ex)
        {
            _windowManager.ShowMessageBox($"Unable to save project:\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    [ICommand]
    public void AddNewProject()
    {
        var projectFileName = _fileSelect.GetNewProjectFileNameByUser();

        try
        {
            if (projectFileName is not null)
            {
                _projectService.CreateNewProject(Path.GetFullPath(projectFileName)).Switch(
                    success =>
                    {
                        var projectVM = new ProjectNodeViewModel((ProjectNode)success.Result.Root);
                        Projects.Add(projectVM);
                        OnPropertyChanged(nameof(HasProject));
                        //_events.PublishOnUIThread(new ProjectLoadedEvent(projectVM.Node.DiskLocation));
                    },
                    fail => _windowManager.ShowMessageBox($"{fail.Reason}", "Project Error"));
            }
        }
        catch (Exception ex)
        {
            _windowManager.ShowMessageBox($"Unable to create new project at location '{projectFileName}'\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    [ICommand]
    public void NewProjectFromFile()
    {
        var dataFileName = _fileSelect.GetExistingDataFileNameByUser();
        if (dataFileName is null)
            return;

        var projectPath = Path.GetDirectoryName(dataFileName);
        var projectFileName = Path.Combine(projectPath, Path.GetFileNameWithoutExtension(dataFileName) + "Project.xml");

        try
        {
            if (dataFileName is not null)
            {
                _projectService.CreateNewProjectWithExistingFile(Path.GetFullPath(projectFileName), Path.GetFullPath(dataFileName)).Switch(
                    success =>
                    {
                        var projectVM = new ProjectNodeViewModel((ProjectNode)success.Result.Root);
                        Projects.Add(projectVM);
                        SelectedNode = projectVM;
                        IsModified = true;

                        OnPropertyChanged(nameof(HasProject));
                        //_events.PublishOnUIThread(new ProjectLoadedEvent(projectVM.Node.DiskLocation));
                    },
                    fail => _windowManager.ShowMessageBox($"{fail.Reason}", "Project Error"));
            }
        }
        catch (Exception ex)
        {
            _windowManager.ShowMessageBox($"Unable to create new project at location '{projectFileName}'\n{ex.Message}\n{ex.StackTrace}");
        }
    }

    public bool OpenProject()
    {
        var projectFileName = _fileSelect.GetProjectFileNameByUser();

        if (projectFileName is null)
            return false;

        return OpenProject(projectFileName);
    }

    public bool OpenProject(string projectFileName)
    {
        if (projectFileName is null)
            return false;

        var openResult = _projectService.OpenProjectFile(projectFileName);

        return openResult.Match(
            success =>
            {
                var projectVM = new ProjectNodeViewModel((ProjectNode)success.Result.Root);
                Projects.Add(projectVM);
                OnPropertyChanged(nameof(HasProject));
                //_events.PublishOnUIThread(new ProjectLoadedEvent(projectFileName));
                return true;
            },
            fail =>
            {
                var message = $"Project '{projectFileName}' contained {fail.Reasons.Count} errors{Environment.NewLine}" +
                    string.Join(Environment.NewLine, fail.Reasons);
                _windowManager.ShowMessageBox(message, "Project Open Error");
                return false;
            });
    }

    public bool SaveProjectAs(ProjectNodeViewModel projectVM)
    {
        var projectTree = _projectService.GetContainingProject(projectVM.Node);

        var newFileName = _fileSelect.GetNewProjectFileNameByUser();

        if (newFileName is null)
            return false;

        return _projectService.SaveProjectAs(projectTree, newFileName).Match(
            success =>
            {
                return true;
            },
            fail =>
            {
                _windowManager.ShowMessageBox(fail.Reason, "Project Save Error");
                return false;
            });
    }

    public bool CloseProject(ProjectNodeViewModel projectVM)
    {
        var projectTree = _projectService.GetContainingProject(projectVM.Node);

        return _projectService.SaveProject(projectTree).Match(
            success =>
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

                var remainingEditors = _editors.Editors
                    .Where(x => !removedEditors.Contains(x));

                if (removedEditors.Any(x => _editors.RequestSaveUserChanges(x, false) == UserSaveAction.Cancel))
                    return false;

                for (int i = 0; i < _editors.Editors.Count; i++)
                {
                    if (!remainingEditors.Contains(_editors.Editors[i]))
                        _editors.Editors.Remove(_editors.Editors[i]);
                }

                _editors.Editors = new(remainingEditors);
                _editors.ActiveEditor = _editors.Editors.FirstOrDefault();

                _projectService.SaveProject(projectTree)
                 .Switch(
                     success => IsModified = false,
                     fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.Root.DiskLocation}: {fail.Reason}")
                 );

                _projectService.CloseProject(projectTree);
                Projects.Remove(projectVM);
                OnPropertyChanged(nameof(HasProject));
                return true;
            },
            fail =>
            {
                _windowManager.ShowMessageBox(fail.Reason, "Project Save Error");
                return false;
            });
    }

    [ICommand]
    public void CloseAllProjects()
    {
        while (Projects.Count > 0)
        {
            if (!CloseProject(Projects.First()))
                return;
        }
    }

    [ICommand]
    public void ExploreResource(ResourceNodeViewModel nodeVM)
    {
        _diskExploreService.ExploreDiskLocation(nodeVM.Node.DiskLocation);
    }

    public override void DiscardChanges()
    {
    }
}