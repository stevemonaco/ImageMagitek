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

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeViewModel : ToolViewModel, IDropTarget, 
        IHandle<AddDataFileEvent>, IHandle<AddPaletteEvent>, IHandle<AddScatteredArrangerEvent>, IHandle<AddScatteredArrangerFromExistingEvent>
    {
        private IProjectService _projectService;
        private IPaletteService _paletteService;
        private IFileSelectService _fileSelect;
        private IEventAggregator _events;
        private IWindowManager _windowManager;
        private Tracker _tracker;

        //private ProjectTree _activeTree;

        public ProjectTreeViewModel(IProjectService solutionService, IPaletteService paletteService,
            IFileSelectService fileSelect, IEventAggregator events, IWindowManager windowManager, Tracker tracker)
        {
            DisplayName = "Project Tree";
            _projectService = solutionService;
            _paletteService = paletteService;
            _fileSelect = fileSelect;
            _tracker = tracker;

            _windowManager = windowManager;
            _events = events;
            _events.Subscribe(this);
        }

        public bool HasProject => Projects.Any();

        private BindableCollection<ImageProjectNodeViewModel> _projects = new BindableCollection<ImageProjectNodeViewModel>();
        public BindableCollection<ImageProjectNodeViewModel> Projects
        {
            get => _projects;
            set => SetAndNotify(ref _projects, value);
        }

        private TreeNodeViewModel _selectedItem;
        public TreeNodeViewModel SelectedItem
        {
            get => _selectedItem;
            set => SetAndNotify(ref _selectedItem, value);
        }

        public void ActivateSelectedItem()
        {
            if (SelectedItem is null)
                return;

            switch(SelectedItem)
            {
                case ImageProjectNodeViewModel project:
                    _events.PublishOnUIThread(new ActivateEditorEvent(project.Node.Value));
                    break;
                case PaletteNodeViewModel pal:
                    _events.PublishOnUIThread(new ActivateEditorEvent(pal.Node.Value));
                    break;
                case ArrangerNodeViewModel arranger:
                    _events.PublishOnUIThread(new ActivateEditorEvent(arranger.Node.Value));
                    break;
                case DataFileNodeViewModel file:
                    _events.PublishOnUIThread(new ActivateEditorEvent(file.Node.Value));
                    break;
                case FolderNodeViewModel folder:
                    _events.PublishOnUIThread(new ActivateEditorEvent(folder.Node.Value));
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(ActivateSelectedItem)} was called with a {nameof(SelectedItem)} of type {SelectedItem.GetType()}");
            }
        }

        public void AddNewFolder(TreeNodeViewModel parentNodeModel)
        {
            var projectTree = _projectService.GetContainingProject(parentNodeModel.Node);

            if (projectTree.CreateNewFolder(parentNodeModel.Node, "New Folder", false).Value is ResourceNode resourceNode)
            {
                var folderVM = new FolderNodeViewModel(resourceNode, parentNodeModel);
                SelectedItem = folderVM;
                IsModified = true;
            }
        }

        public void AddNewDataFile(TreeNodeViewModel parentNodeModel) =>
            _events.PublishOnUIThread(new AddDataFileEvent(parentNodeModel));

        public void AddNewPalette(TreeNodeViewModel parentNodeModel) =>
            _events.PublishOnUIThread(new AddPaletteEvent(parentNodeModel));

        public void AddNewScatteredArranger(TreeNodeViewModel parentNodeModel) =>
            _events.PublishOnUIThread(new AddScatteredArrangerEvent(parentNodeModel));

        public void ExportArrangerAs(TreeNodeViewModel nodeModel)
        {
            if (nodeModel is ArrangerNodeViewModel arrNodeModel)
            {
                var arranger = arrNodeModel.Node.Value as ScatteredArranger;
                var exportFileName = _fileSelect.GetExportArrangerFileNameByUser($"{arranger.Name}.png");

                if (exportFileName is object)
                {
                    if (arranger.ColorType == PixelColorType.Indexed)
                    {
                        var image = new IndexedImage(arranger, _paletteService.DefaultPalette);
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

        public void ImportImageAs(TreeNodeViewModel nodeModel)
        {
            if (nodeModel is ArrangerNodeViewModel arrNodeModel)
            {
                //var arranger = arrNodeModel.Node.Value as ScatteredArranger;
                //var importFileName = _fileSelect.GetImportArrangerFileNameByUser();

                //if (importFileName is object)
                //{
                //    if (arranger.ColorType == PixelColorType.Indexed)
                //    {
                //        var image = new IndexedImage(arranger, _paletteService.DefaultPalette);
                //        image.ImportImage(importFileName, new ImageFileAdapter(), ColorMatchStrategy.Exact);
                //        image.SaveImage();
                //    }
                //    else if (arranger.ColorType == PixelColorType.Direct)
                //    {
                //        var image = new DirectImage(arranger);
                //        image.ImportImage(importFileName, new ImageFileAdapter());
                //        image.SaveImage();
                //    }
                //}

                var arranger = arrNodeModel.Node.Value as ScatteredArranger;
                var model = new ImportImageViewModel(arranger, _paletteService, _fileSelect);

                _windowManager.ShowDialog(model);
            }
        }

        public void RequestRemoveNode(TreeNodeViewModel nodeModel)
        {
            var eventModel = new RequestRemoveTreeNodeEvent(nodeModel);
            _events.PublishOnUIThread(eventModel);
        }

        public void RequestRenameNode(Tuple<string, object> renameTuple)
        {
            if (renameTuple.Item1 is string name && renameTuple.Item2 is TreeNodeViewModel model)
            {
                if (model.ParentModel.Node.ContainsChild(name))
                {
                    _windowManager.ShowMessageBox($"Parent item already contains an item named '{name}'", icon: MessageBoxImage.Error);
                }
                else
                {
                    model.Name = name;
                    IsModified = true;
                }
            }
        }

        public void RenameNode(TreeNodeViewModel nodeModel)
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

                    IsModified = true;
                    var renameEvent = new ResourceRenamedEvent(nodeModel.Node.Value, newName, oldName);
                    _events.PublishOnUIThread(renameEvent);
                }
            }
        }

        public void ApplyRemovalChanges(ResourceRemovalChangesViewModel changes)
        {
            foreach (var item in changes.ChangedResources)
            {
                foreach (var removeItem in changes.RemovedResources)
                {
                    item.Resource.UnlinkResource(removeItem.Resource);
                }
            }

            foreach (var item in changes.RemovedResources)
            {
                var parent = item.ModelNode.ParentModel;
                parent.Children.Remove(item.ModelNode);

                var resourceNode = item.Resource;
                var resourceParent = item.ResourceNode.Parent;
                resourceParent.RemoveChild(resourceNode.Name);
            }
        }

        private void UnloadProjects()
        {
            SelectedItem = null;
            _projectService.CloseProjects(false);
            Projects.Clear();
            NotifyOfPropertyChange(() => HasProject);
        }

        public void Handle(AddDataFileEvent message)
        {
            var dataFileName = _fileSelect.GetExistingDataFileNameByUser();

            if (dataFileName is object)
            {
                var parentModel = message.Parent ?? Projects.First();
                var dfName = Path.GetFileName(dataFileName);
                var projectTree = _projectService.GetContainingProject(parentModel.Node);

                if (parentModel.Children.Any(x => x.Name == dfName))
                {
                    _windowManager.ShowMessageBox($"'{parentModel.Name}' already contains a resource named '{dfName}'", "Error");
                    return;
                }

                var df = new DataFile(dfName, dataFileName);
                var result = projectTree.AddResource(parentModel.Node, df);

                result.Switch(success =>
                {
                    var dfVM = new DataFileNodeViewModel(success.Result, parentModel);
                    SelectedItem = dfVM;
                    IsModified = true;
                },
                fail =>
                {
                    _windowManager.ShowMessageBox(fail.Reason, "Resource Error");
                });
            }
        }

        public void Handle(AddPaletteEvent message)
        {
            var parentModel = message.Parent ?? Projects.First();
            var dialogModel = new AddPaletteViewModel(parentModel.Children.Select(x => x.Name));

            var projectTree = _projectService.GetContainingProject(parentModel.Node);
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

            if(_windowManager.ShowDialog(dialogModel) is true)
            {
                var pal = new Palette(dialogModel.PaletteName, Palette.StringToColorModel(dialogModel.SelectedColorModel), new FileBitAddress(dialogModel.FileOffset, 0),
                    dialogModel.Entries, dialogModel.ZeroIndexTransparent, PaletteStorageSource.DataFile);
                pal.DataFile = dialogModel.SelectedDataFile;

                var result = projectTree.AddResource(parentModel.Node, pal);

                result.Switch(success =>
                {
                    var palVM = new PaletteNodeViewModel(success.Result, parentModel);
                    SelectedItem = palVM;
                    IsModified = true;
                    _tracker.Persist(dialogModel);
                },
                fail =>
                {
                    _windowManager.ShowMessageBox(fail.Reason, "Resource Error");
                });
            }
        }

        public void Handle(AddScatteredArrangerEvent message)
        {
            var parentModel = message.Parent ?? Projects.First();
            var dialogModel = new AddScatteredArrangerViewModel(parentModel.Children.Select(x => x.Name));
            var projectTree = _projectService.GetContainingProject(parentModel.Node);
            _tracker.Track(dialogModel);

            if (_windowManager.ShowDialog(dialogModel) is true)
            {
                var arranger = new ScatteredArranger(dialogModel.ArrangerName, dialogModel.ColorType, 
                    dialogModel.Layout, dialogModel.ArrangerElementWidth, dialogModel.ArrangerElementHeight, 
                    dialogModel.ElementPixelWidth, dialogModel.ElementPixelHeight);

                var result = projectTree.AddResource(parentModel.Node, arranger);

                result.Switch(success =>
                {
                    var arrangerVM = new ArrangerNodeViewModel(success.Result, parentModel);
                    SelectedItem = arrangerVM;
                    IsModified = true;
                    _tracker.Persist(dialogModel);
                },
                fail =>
                {
                    _windowManager.ShowMessageBox(fail.Reason, "Resource Error");
                });
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
                        var result = projectTree.AddResource(parentModel.Node, newArranger);
                        var arrangerVM = new ArrangerNodeViewModel(result.AsT0.Result, parentModel);
                        SelectedItem = arrangerVM;
                        IsModified = true;
                    },
                    fail => _windowManager.ShowMessageBox($"{fail.Reason}", "Error")
                );
            }
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is TreeNodeViewModel sourceModel && dropInfo.TargetItem is TreeNodeViewModel targetModel)
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
            var targetModel = dropInfo.TargetItem as TreeNodeViewModel;

            if (dropInfo.Data is TreeNodeViewModel sourceModel && (targetModel is ImageProjectNodeViewModel || targetModel is FolderNodeViewModel))
            {
                var projectTree = _projectService.GetContainingProject(sourceModel.Node);

                var result = projectTree.MoveNode(sourceModel.Node, targetModel.Node);

                result.Switch(
                    success =>
                    {
                        IsModified = true;
                        SelectedItem = sourceModel;
                    },
                    fail => _windowManager.ShowMessageBox($"{fail.Reason}", "Move Error")
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

        public MagitekResult NewProject(string newFileName)
        {
            var newResult = _projectService.NewProject(Path.GetFileName(newFileName));

            return newResult.Match<MagitekResult>(
                success =>
                {
                    Projects.Clear();
                    var projectVM = new ImageProjectNodeViewModel(success.Result.Tree.Root as ResourceNode);
                    Projects.Add(projectVM);
                    return new MagitekResult.Success();
                },
                fail => new MagitekResult.Failed(fail.Reason));
        }

        public bool OpenProject(string projectFileName)
        {
            var openResult = _projectService.OpenProjectFile(projectFileName);

            return openResult.Match(
                success =>
                {
                    var projectVM = new ImageProjectNodeViewModel(success.Result.Tree.Root as ResourceNode);
                    Projects.Add(projectVM);
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

        public void CloseProject() => UnloadProjects();

        public override void DiscardChanges()
        {
        }
    }
}
