using System;
using System.Windows;
using System.IO;
using System.Linq;
using GongSolutions.Wpf.DragDrop;
using Stylet;
using TileShop.Shared.EventModels;
using TileShop.WPF.Services;
using TileShop.WPF.ViewModels.Dialogs;
using TileShop.WPF.EventModels;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Services;
using Jot;
using Point = System.Drawing.Point;
using ImageMagitek.Project;
using Monaco.PathTree;
using System.Collections.Generic;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeViewModel : ToolViewModel, IDropTarget, IHandle<AddScatteredArrangerFromExistingEvent>
    {
        private readonly IProjectService _projectService;
        private readonly IPaletteService _paletteService;
        private readonly IFileSelectService _fileSelect;
        private readonly IEventAggregator _events;
        private readonly IWindowManager _windowManager;
        private readonly Tracker _tracker;
        private readonly EditorsViewModel _editors;

        public ProjectTreeViewModel(IProjectService solutionService, IPaletteService paletteService,
            IFileSelectService fileSelect, IEventAggregator events, IWindowManager windowManager,
            Tracker tracker, EditorsViewModel editors)
        {
            _projectService = solutionService;
            _paletteService = paletteService;
            _fileSelect = fileSelect;
            _windowManager = windowManager;
            _tracker = tracker;
            _editors = editors;

            _events = events;
            _events.Subscribe(this);

            DisplayName = "Project Tree";
        }

        public bool HasProject => Projects.Any();

        private BindableCollection<ProjectNodeViewModel> _projects = new BindableCollection<ProjectNodeViewModel>();
        public BindableCollection<ProjectNodeViewModel> Projects
        {
            get => _projects;
            set => SetAndNotify(ref _projects, value);
        }

        private ResourceNodeViewModel _selectedNode;
        public ResourceNodeViewModel SelectedNode
        {
            get => _selectedNode;
            set => SetAndNotify(ref _selectedNode, value);
        }

        private readonly Dictionary<MessageBoxResult, string> messageBoxLabels = new Dictionary<MessageBoxResult, string>
        {
            { MessageBoxResult.Yes, "Save" }, { MessageBoxResult.No, "Discard" }, { MessageBoxResult.Cancel, "Cancel" }
        };

        public void ActivateSelectedNode()
        {
            if (SelectedNode is ProjectNodeViewModel || SelectedNode is FolderNodeViewModel)
            {
                SelectedNode.IsExpanded ^= true;
            }
            else if (SelectedNode?.Node?.Value is object)
            {
                _editors.ActivateEditor(SelectedNode.Node.Value);
            }
        }

        public void AddNewFolder(ResourceNodeViewModel parentNodeModel)
        {
            var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);

            projectTree.CreateNewFolder(parentNodeModel.Node, "New Folder", false).Switch(
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

        public void AddNewDataFile(ResourceNodeViewModel parentNodeModel)
        {
            var dataFileName = _fileSelect.GetExistingDataFileNameByUser();

            if (dataFileName is object)
            {
                var dfName = Path.GetFileName(dataFileName);
                var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);

                if (parentNodeModel.Children.Any(x => x.Name == dfName))
                {
                    _windowManager.ShowMessageBox($"'{parentNodeModel.Name}' already contains a resource named '{dfName}'", "Error");
                    return;
                }

                var df = new DataFile(dfName, dataFileName);
                var result = projectTree.AddResource(parentNodeModel.Node, df);

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

        public void AddNewPalette(ResourceNodeViewModel parentNodeModel)
        {
            var dialogModel = new AddPaletteViewModel(parentNodeModel.Children.Select(x => x.Name));

            var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);
            var dataFiles = projectTree.Tree.EnumerateDepthFirst().Select(x => x.Value).OfType<DataFile>();
            dialogModel.DataFiles.AddRange(dataFiles);
            dialogModel.SelectedDataFile = dialogModel.DataFiles.FirstOrDefault();
            dialogModel.ColorModels.AddRange(Palette.GetColorModelNames());

            _tracker.Track(dialogModel);

            if (dialogModel.DataFiles.Count == 0)
            {
                _windowManager.ShowMessageBox("Project does not contain any data files to define a palette", "Project Error");
                return;
            }

            if (_windowManager.ShowDialog(dialogModel) is true)
            {
                var pal = new Palette(dialogModel.PaletteName, Palette.StringToColorModel(dialogModel.SelectedColorModel), new FileBitAddress(dialogModel.FileOffset, 0),
                    dialogModel.Entries, dialogModel.ZeroIndexTransparent, PaletteStorageSource.DataFile);
                pal.DataFile = dialogModel.SelectedDataFile;

                var result = projectTree.AddResource(parentNodeModel.Node, pal);

                result.Switch(success =>
                {
                    var palVM = new PaletteNodeViewModel(success.Result, parentNodeModel);
                    parentNodeModel.Children.Add(palVM);
                    SelectedNode = palVM;
                    IsModified = true;
                    _tracker.Persist(dialogModel);
                },
                fail =>
                {
                    _windowManager.ShowMessageBox(fail.Reason, "Resource Error");
                });
            }
        }

        public void AddNewScatteredArranger(ResourceNodeViewModel parentNodeModel)
        {
            var dialogModel = new AddScatteredArrangerViewModel(parentNodeModel.Children.Select(x => x.Name));
            var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);
            _tracker.Track(dialogModel);

            if (_windowManager.ShowDialog(dialogModel) is true)
            {
                var arranger = new ScatteredArranger(dialogModel.ArrangerName, dialogModel.ColorType,
                    dialogModel.Layout, dialogModel.ArrangerElementWidth, dialogModel.ArrangerElementHeight,
                    dialogModel.ElementPixelWidth, dialogModel.ElementPixelHeight);

                var result = projectTree.AddResource(parentNodeModel.Node, arranger);

                result.Switch(success =>
                {
                    var arrangerVM = new ArrangerNodeViewModel(success.Result, parentNodeModel);
                    parentNodeModel.Children.Add(arrangerVM);
                    SelectedNode = arrangerVM;
                    IsModified = true;
                    _tracker.Persist(dialogModel);
                },
                fail =>
                {
                    _windowManager.ShowMessageBox(fail.Reason, "Resource Error");
                });
            }
        }

        public void ExportArrangerAs(ResourceNodeViewModel nodeModel)
        {
            if (nodeModel is ArrangerNodeViewModel arrNodeModel)
            {
                var arranger = arrNodeModel.Node.Value as ScatteredArranger;
                var exportFileName = _fileSelect.GetExportArrangerFileNameByUser($"{arranger.Name}.png");

                if (exportFileName is object)
                {
                    if (arranger.ColorType == PixelColorType.Indexed)
                    {
                        var image = new IndexedImage(arranger);
                        image.ExportImage(exportFileName, new ImageFileAdapter());
                    }
                    else if (arranger.ColorType == PixelColorType.Direct)
                    {
                        var image = new DirectImage(arranger);
                        image.ExportImage(exportFileName, new ImageFileAdapter());
                    }
                }
            }
        }

        public void ImportImageAs(ResourceNodeViewModel nodeModel)
        {
            if (nodeModel is ArrangerNodeViewModel arrNodeModel && arrNodeModel.Node.Value is ScatteredArranger arranger)
            {
                var model = new ImportImageViewModel(arranger, _paletteService, _fileSelect);
                _windowManager.ShowDialog(model);
            }
        }

        public void RequestRemoveNode(ResourceNodeViewModel nodeModel)
        {
            var removeNode = nodeModel.Node;
            var projectTree = _projectService.GetContainingProject(removeNode);
            var changes = projectTree.GetSecondaryResourceRemovalChanges(removeNode).ToList();

            var changeVm = new ResourceRemovalChangesViewModel(new ResourceChangeViewModel(removeNode, true, false, false), 
                changes.Select(x => new ResourceChangeViewModel(x)).ToList());

            bool? result;
            result = _windowManager.ShowDialog(changeVm);

            if (result is true)
            {
                var modifiedEditors = _editors.Editors.Where(x => x.IsModified); //.Concat(Tools.OfType<ArrangerEditorViewModel>().Where(x => x.IsModified));

                if (modifiedEditors.Any())
                {
                    var boxResult = _windowManager.ShowMessageBox("The project contains modified items which must be saved or discarded before removing any items", "Save changes",
                        MessageBoxButton.YesNoCancel, buttonLabels: messageBoxLabels);

                    if (boxResult == MessageBoxResult.Yes)
                    {
                        foreach (var editor in modifiedEditors)
                            editor.SaveChanges();
                    }
                    else if (boxResult == MessageBoxResult.No)
                    {
                        foreach (var editor in modifiedEditors)
                            editor.DiscardChanges();
                    }
                    else if (boxResult == MessageBoxResult.Cancel)
                        return;
                }
                
                _editors.Editors.Clear();
                _editors.ActiveEditor = null;

                projectTree.ApplyRemovalChanges(changes);
                var projectRootNode = projectTree.Tree.Root as ResourceNode;
                var projectRootVm = Projects.First(x => ReferenceEquals(projectRootNode, x.Node));
                SynchronizeTree(projectRootVm);

                _projectService.SaveProject(projectTree)
                .Switch(
                    success => IsModified = false,
                    fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.FileLocation}: {fail.Reason}")
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
            if (resourceNode.Children is null)
                return;

            if (!resourceNode.Children.All(x => vmNode.Children.Any(y => ReferenceEquals(x, y.Node))) && 
                resourceNode.Children.Count() == vmNode.Children.Count)
                return;

            SynchronizeDeletions(resourceNode, vmNode);
            SynchronizeInsertions(resourceNode, vmNode);

            void SynchronizeDeletions(ResourceNode resourceNode, ResourceNodeViewModel vmNode)
            {
                List<ResourceNodeViewModel> removedItems = default;

                foreach (var vm in vmNode.Children)
                {
                    if (!resourceNode.Children.Any(x => ReferenceEquals(x, vm.Node)))
                    {
                        if (removedItems is null)
                            removedItems = new List<ResourceNodeViewModel>();

                        removedItems.Add(vm);
                    }
                }

                if (removedItems is object)
                {
                    foreach (var vm in removedItems)
                        vmNode.Children.Remove(vm);
                }
            }

            void SynchronizeInsertions(ResourceNode resourceNode, ResourceNodeViewModel vmNode)
            {
                foreach (var node in resourceNode.Children)
                {
                    if (!vmNode.Children.Any(x => ReferenceEquals(node, x.Node)))
                    {
                        ResourceNodeViewModel newNode = node switch
                        {
                            FolderNode _ => new FolderNodeViewModel(node, vmNode),
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

        public void RenameNode(ResourceNodeViewModel nodeModel)
        {
            var dialogModel = new RenameNodeViewModel(nodeModel);
            var result = _windowManager.ShowDialog(dialogModel);

            if (result is true)
            {
                var oldName = nodeModel.Name;
                var newName = dialogModel.Name;

                if (nodeModel.ParentModel is object && nodeModel.ParentModel.Node.ContainsChild(newName))
                {
                    _windowManager.ShowMessageBox($"Parent item already contains an item named '{newName}'", icon: MessageBoxImage.Error);
                }
                else
                {
                    nodeModel.Node.Rename(newName);
                    nodeModel.Node.Value.Name = newName;
                    nodeModel.Name = newName;

                    var projectTree = _projectService.GetContainingProject(nodeModel.Node);

                    _projectService.SaveProject(projectTree)
                    .Switch(
                        success => IsModified = false,
                        fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.FileLocation}: {fail.Reason}")
                    );

                    var renameEvent = new ResourceRenamedEvent(nodeModel.Node.Value, newName, oldName);
                    _events.PublishOnUIThread(renameEvent);
                }
            }
        }

        public void Handle(AddScatteredArrangerFromExistingEvent message)
        {
            var parentModel = Projects.First();
            var model = new NameResourceViewModel();
            var arranger = message.Arranger;
            var projectTree = _projectService.GetContainingProject(parentModel.Node);

            if (_windowManager.ShowDialog(model) is true)
            {
                var newArranger = new ScatteredArranger(model.ResourceName, arranger.ColorType, arranger.Layout, message.Width, message.Height, arranger.ElementPixelSize.Width, arranger.ElementPixelSize.Height);
                var source = new Point(message.ElementX, message.ElementY);
                var dest = new Point(0, 0);

                var result = ElementCopier.CopyElements(arranger, newArranger, source, dest, message.Width, message.Height);

                result.Switch(
                    success =>
                    {
                        var nodeResult = projectTree.AddResource(parentModel.Node, newArranger);
                        var arrangerVM = new ArrangerNodeViewModel(nodeResult.AsT0.Result, parentModel);
                        parentModel.Children.Add(arrangerVM);
                        SelectedNode = arrangerVM;
                        IsModified = true;
                        _editors.ActivateEditor(newArranger);
                    },
                    fail => _windowManager.ShowMessageBox($"{fail.Reason}", "Error")
                );
            }
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ResourceNodeViewModel sourceModel && dropInfo.TargetItem is ResourceNodeViewModel targetModel)
            {
                var projectTree = _projectService.GetContainingProject(sourceModel.Node);
                projectTree.CanMoveNode(sourceModel.Node, targetModel.Node).Switch(
                    success =>
                    {
                        dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                        dropInfo.Effects = DragDropEffects.Move;
                    },
                    fail => { }
                );
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            var targetModel = dropInfo.TargetItem as ResourceNodeViewModel;

            if (dropInfo.Data is ResourceNodeViewModel sourceModel && (targetModel is ResourceNodeViewModel || targetModel is FolderNodeViewModel))
            {
                var projectTree = _projectService.GetContainingProject(sourceModel.Node);

                var result = projectTree.MoveNode(sourceModel.Node, targetModel.Node);

                result.Switch(
                    success =>
                    {
                        sourceModel.ParentModel.Children.Remove(sourceModel);
                        sourceModel.ParentModel = targetModel;
                        targetModel.Children.Add(sourceModel);
                        SelectedNode = sourceModel;

                        _projectService.SaveProject(projectTree)
                        .Switch(
                            success => IsModified = false,
                            fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.FileLocation}: {fail.Reason}")
                        );
                    },
                    fail => _windowManager.ShowMessageBox($"{fail.Reason}", "Move Resource Error")
                    );
            }
        }

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
                             fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.FileLocation}: {fail.Reason}")
                         );
                }
            }
            catch (Exception ex)
            {
                _windowManager.ShowMessageBox($"Unable to save project:\n{ex.Message}\n{ex.StackTrace}");
            }
        }

        public void AddNewProject()
        {
            var projectFileName = _fileSelect.GetNewProjectFileNameByUser();

            try
            {
                if (projectFileName is object)
                {
                    _projectService.NewProject(Path.GetFullPath(projectFileName)).Switch(
                        success =>
                        {
                            var projectVM = new ProjectNodeViewModel((ProjectNode)success.Result.Tree.Root);
                            Projects.Add(projectVM);
                            NotifyOfPropertyChange(() => HasProject);
                            _events.PublishOnUIThread(new ProjectLoadedEvent());
                        },
                        fail => _windowManager.ShowMessageBox($"{fail.Reason}", "Project Error"));
                }
            }
            catch (Exception ex)
            {
                _windowManager.ShowMessageBox($"Unable to create new project at location '{projectFileName}'\n{ex.Message}\n{ex.StackTrace}");
                // TODO: Log
            }
        }

        public bool OpenProject()
        {
            var projectFileName = _fileSelect.GetProjectFileNameByUser();

            if (projectFileName is null)
                return false;

            var openResult = _projectService.OpenProjectFile(projectFileName);

            return openResult.Match(
                success =>
                {
                    var projectVM = new ProjectNodeViewModel((ProjectNode)success.Result.Tree.Root);
                    Projects.Add(projectVM);
                    NotifyOfPropertyChange(() => HasProject);
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

            projectTree.FileLocation = newFileName;

            return _projectService.SaveProject(projectTree).Match(
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
                    var removedEditors = new HashSet<ResourceEditorBaseViewModel>();
                    removedEditors.UnionWith(_editors.Editors.Where(x => projectTree.ContainsResource(x.Resource)));
                    removedEditors.UnionWith(_editors.Editors.OfType<SequentialArrangerEditorViewModel>().Where(x => projectTree.ContainsResource(((SequentialArranger)x.Resource).ActiveDataFile)));

                    var remainingEditors = _editors.Editors
                        .Where(x => !removedEditors.Contains(x));

                    if (!removedEditors.All(x => _editors.RequestSaveUserChanges(x, false)))
                        return false;

                    _editors.Editors = new BindableCollection<ResourceEditorBaseViewModel>(remainingEditors);
                    _editors.ActiveEditor = _editors.Editors.FirstOrDefault();

                    if (!_editors.ClosePixelEditor())
                        return false;

                    _projectService.SaveProject(projectTree)
                     .Switch(
                         success => IsModified = false,
                         fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.FileLocation}: {fail.Reason}")
                     );

                    _projectService.CloseProject(projectTree);
                    Projects.Remove(projectVM);
                    NotifyOfPropertyChange(() => HasProject);
                    return true;
                },
                fail =>
                {
                    _windowManager.ShowMessageBox(fail.Reason, "Project Save Error");
                    return false;
                });
        }

        public void CloseAllProjects()
        {
            while (Projects.Count > 0)
            {
                if (!CloseProject(Projects.First()))
                    return;
            }
        }

        public override void DiscardChanges()
        {
        }
    }
}
