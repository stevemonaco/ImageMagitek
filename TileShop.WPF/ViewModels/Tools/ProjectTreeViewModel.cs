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
using TileShop.Shared.Services;
using Jot;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeViewModel : ToolViewModel, IDropTarget, 
        IHandle<AddDataFileEvent>, IHandle<AddPaletteEvent>, IHandle<AddScatteredArrangerEvent>
    {
        private IProjectTreeService _treeService;
        private IPaletteService _paletteService;
        private IFileSelectService _fileSelect;
        private IEventAggregator _events;
        private IWindowManager _windowManager;
        private Tracker _tracker;

        public ProjectTreeViewModel(IProjectTreeService treeService, IPaletteService paletteService,
            IFileSelectService fileSelect, IEventAggregator events, IWindowManager windowManager, Tracker tracker)
        {
            DisplayName = "Project Tree";
            _treeService = treeService;
            _paletteService = paletteService;
            _fileSelect = fileSelect;
            _tracker = tracker;

            _windowManager = windowManager;
            _events = events;
            _events.Subscribe(this);
        }

        public bool HasProject => ProjectFileName is object;

        private string _projectFileName;
        public string ProjectFileName
        {
            get => _projectFileName;
            set => SetAndNotify(ref _projectFileName, value);
        }

        private BindableCollection<ImageProjectNodeViewModel> _projectRoot = new BindableCollection<ImageProjectNodeViewModel>();
        public BindableCollection<ImageProjectNodeViewModel> ProjectRoot
        {
            get => _projectRoot;
            set => SetAndNotify(ref _projectRoot, value);
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
            if (_treeService.CreateNewFolder(parentNodeModel) is TreeNodeViewModel model)
            {
                SelectedItem = model;
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
                var exportFileName = _fileSelect.GetExportArrangerFileNameByUser($"{arranger.Name}.bmp");

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

        private void UnloadProject()
        {
            ProjectFileName = null;
            SelectedItem = null;
            _treeService.UnloadProject();

            ProjectRoot.Clear();
            NotifyOfPropertyChange(() => HasProject);
        }

        public void Handle(AddDataFileEvent message)
        {
            var dataFileName = _fileSelect.GetExistingDataFileNameByUser();

            if (dataFileName is object)
            {
                var parentModel = message.Parent ?? ProjectRoot.First();
                if (parentModel.Children.Any(x => x.Name == dataFileName))
                {
                    _windowManager.ShowDialog($"'{parentModel.Name}' already contains a resource named '{dataFileName}'");
                    return;
                }

                DataFile df = new DataFile(Path.GetFileName(dataFileName), dataFileName);
                var node = _treeService.AddResource(parentModel, df);
                SelectedItem = node;
                IsModified = true;
            }
        }

        public void Handle(AddPaletteEvent message)
        {
            var parentModel = message.Parent ?? ProjectRoot.First();

            var model = new AddPaletteViewModel();

            var dataFiles = _treeService.Tree.EnumerateDepthFirst().Select(x => x.Value).OfType<DataFile>();
            model.DataFiles.AddRange(dataFiles);
            model.SelectedDataFile = model.DataFiles.FirstOrDefault();
            model.ColorModels.AddRange(Palette.GetColorModelNames());

            _tracker.Track(model);

            if (model.DataFiles.Count == 0)
            {
                _windowManager.ShowMessageBox("Project does not contain any data files to define a palette", "Project Error");
                return;
            }

            if(_windowManager.ShowDialog(model) is true)
            {
                var pal = new Palette(model.PaletteName, Palette.StringToColorModel(model.SelectedColorModel), new FileBitAddress(model.FileOffset, 0),
                    model.Entries, model.ZeroIndexTransparent, PaletteStorageSource.DataFile);
                pal.DataFile = model.SelectedDataFile;

                var node = _treeService.AddResource(parentModel, pal);
                SelectedItem = node;
                IsModified = true;
                _tracker.Persist(model);
            }
        }

        public void Handle(AddScatteredArrangerEvent message)
        {
            var parentModel = message.Parent ?? ProjectRoot.First();
            var model = new AddScatteredArrangerViewModel(parentModel.Children.Select(x => x.Name));
            _tracker.Track(model);

            if (_windowManager.ShowDialog(model) is true)
            {
                var arranger = new ScatteredArranger(model.ArrangerName, model.ColorType, 
                    model.Layout, model.ArrangerElementWidth, model.ArrangerElementHeight, 
                    model.ElementPixelWidth, model.ElementPixelHeight);

                var node = _treeService.AddResource(parentModel, arranger);
                SelectedItem = node;
                IsModified = true;
                _tracker.Persist(model);
            }
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is TreeNodeViewModel sourceModel && dropInfo.TargetItem is TreeNodeViewModel targetModel)
            {
                if (_treeService.CanMoveNode(sourceModel, targetModel))
                {
                    dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                    dropInfo.Effects = DragDropEffects.Move;
                }
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            var targetModel = dropInfo.TargetItem as TreeNodeViewModel;

            if (dropInfo.Data is TreeNodeViewModel sourceModel && (targetModel is ImageProjectNodeViewModel || targetModel is FolderNodeViewModel))
            {
                _treeService.MoveNode(sourceModel, targetModel);
                IsModified = true;
                SelectedItem = sourceModel;
            }
        }

        public override void SaveChanges()
        {
            try
            {
                if (_treeService.SaveProject(ProjectFileName))
                    IsModified = false;
                else
                    _windowManager.ShowMessageBox($"An unspecified error occurred while saving the project tree to {ProjectFileName}");                
            }
            catch (Exception ex)
            {
                _windowManager.ShowMessageBox($"Unable to save project '{ProjectFileName}'\n{ex.Message}\n{ex.StackTrace}");
            }
        }

        public void NewProject(string newFileName)
        {
            var project = _treeService.NewProject(Path.GetFileName(newFileName));
            _treeService.SaveProject(newFileName);

            ProjectFileName = newFileName;
            ProjectRoot.Clear();
            ProjectRoot.Add(project);
        }

        public void OpenProject(string projectFileName)
        {
            ProjectRoot.Clear();
            var project = _treeService.OpenProject(projectFileName);
            ProjectRoot.Add(project);
            ProjectFileName = projectFileName;
        }

        public void CloseProject() => UnloadProject();

        public override void DiscardChanges()
        {
        }
    }
}
