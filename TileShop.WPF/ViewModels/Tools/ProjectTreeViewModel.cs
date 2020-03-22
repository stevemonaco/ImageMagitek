using ImageMagitek.Project;
using ImageMagitek;
using Monaco.PathTree;
using System;
using System.Collections.Generic;
using Stylet;
using ImageMagitek.Colors;
using TileShop.Shared.EventModels;
using TileShop.WPF.Services;
using TileShop.Shared.Services;
using System.IO;
using System.Linq;
using GongSolutions.Wpf.DragDrop;
using System.Windows;
using TileShop.WPF.ViewModels.Dialogs;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeViewModel : ToolViewModel, IDropTarget, 
        IHandle<AddDataFileEvent>, IHandle<AddPaletteEvent>, IHandle<AddScatteredArrangerEvent>
    {
        private IProjectTreeService _treeService;
        private IEventAggregator _events;
        private IWindowManager _windowManager;
        private IFileSelectService _fileSelect;

        public ProjectTreeViewModel(IProjectTreeService treeService, IFileSelectService fileSelect, IEventAggregator events, IWindowManager windowManager)
        {
            DisplayName = "Project Tree";
            _treeService = treeService;
            _fileSelect = fileSelect;

            _windowManager = windowManager;
            _events = events;
            _events.Subscribe(this);
        }

        public bool HasProject => ProjectFileName is object;

        private string _ProjectFileName;
        public string ProjectFileName
        {
            get => _ProjectFileName;
            set => SetAndNotify(ref _ProjectFileName, value);
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

        public void CreateNewFolder(TreeNodeViewModel parentNodeModel)
        {
            if (_treeService.CreateNewFolder(parentNodeModel) is TreeNodeViewModel model)
            {
                SelectedItem = model;
                IsModified = true;
            }
        }

        public void RemoveNode(TreeNodeViewModel nodeModel)
        {
            var changes = _treeService.GetResourceRemovalChanges(nodeModel);
            var removedItem = changes.First(x => ReferenceEquals(x.Resource, nodeModel.Node.Value));

            var changeVm = new ResourceRemovalChangesViewModel(removedItem, changes);

            var result = _windowManager.ShowDialog(changeVm);

            if (result is true)
            {
                foreach (var item in changes.Where(x => x.Removed))
                {

                }

                foreach (var item in changes.Where(x => !x.Removed))
                {

                }
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
                DataFile df = new DataFile(Path.GetFileName(dataFileName), dataFileName);
                var node = _treeService.AddResource(df);
                var nodeVm = new DataFileNodeViewModel(node);
                var parentModel = ProjectRoot.First();
                nodeVm.ParentModel = parentModel;
                parentModel.Children.Add(nodeVm);
                IsModified = true;
            }
        }

        public void Handle(AddPaletteEvent message)
        {
            var model = new AddPaletteViewModel();

            var dataFiles = _treeService.Tree.EnumerateDepthFirst().Select(x => x.Value).OfType<DataFile>();
            model.DataFiles.AddRange(dataFiles);
            model.SelectedDataFile = model.DataFiles.FirstOrDefault();

            model.ColorModels.AddRange(Palette.GetColorModelNames());
            model.SelectedColorModel = model.ColorModels.First();
            model.Entries = 2;

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

                var node = _treeService.AddResource(pal);
                var nodeVm = new PaletteNodeViewModel(node);
                var parentModel = ProjectRoot.First();
                nodeVm.ParentModel = parentModel;
                parentModel.Children.Add(nodeVm);
                IsModified = true;
            }
        }

        public void Handle(AddScatteredArrangerEvent message)
        {
            var model = new AddScatteredArrangerViewModel();

            if (_windowManager.ShowDialog(model) is true)
            {
                var arranger = new ScatteredArranger(model.ArrangerName, model.ColorType, 
                    model.Layout, model.ArrangerElementWidth, model.ArrangerElementHeight, 
                    model.ElementPixelWidth, model.ElementPixelHeight);

                var node = _treeService.AddResource(arranger);
                var nodeVm = new ArrangerNodeViewModel(node);
                var parentModel = ProjectRoot.First();
                nodeVm.ParentModel = parentModel;
                parentModel.Children.Add(nodeVm);
                IsModified = true;
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
            var sourceModel = dropInfo.Data as TreeNodeViewModel;
            var targetModel = dropInfo.TargetItem as FolderNodeViewModel;

            if (sourceModel is object && targetModel is object)
            {
                _treeService.MoveNode(sourceModel, targetModel);
                IsModified = true;
                SelectedItem = sourceModel;
            }
        }

        public override void SaveChanges()
        {
            _treeService.SaveProject(ProjectFileName);
            IsModified = false;
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

        public bool TrySaveProject(string projectFileName)
        {
            try
            {
                if (!_treeService.SaveProject(projectFileName))
                {
                    _windowManager.ShowMessageBox($"An unspecified error occurred while saving the project tree to {projectFileName}");
                    return false;
                }
                IsModified = false;
                return true;
            }
            catch(Exception ex)
            {
                _windowManager.ShowMessageBox($"Unable to save project '{projectFileName}'\n{ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public override void DiscardChanges()
        {
        }
    }
}
