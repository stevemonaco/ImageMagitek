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

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeViewModel : ToolViewModel, IDropTarget, 
        IHandle<AddDataFileEvent>, IHandle<AddPaletteEvent>
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

        private BindableCollection<ProjectTreeNodeViewModel> _rootItems = new BindableCollection<ProjectTreeNodeViewModel>();
        public BindableCollection<ProjectTreeNodeViewModel> RootItems
        {
            get => _rootItems;
            set => SetAndNotify(ref _rootItems, value);
        }

        private ProjectTreeImageProjectViewModel _treeRoot;
        public ProjectTreeImageProjectViewModel TreeRoot
        {
            get => _treeRoot;
            set => SetAndNotify(ref _treeRoot, value);
        }

        //public IEnumerable<ProjectTreeNodeViewModel> RootItems
        //{
        //    get
        //    {
        //        if (_treeService.Tree is null)
        //            yield break;

        //        foreach (var node in _treeService.Tree.Children())
        //        {
        //            if (node.Value is ImageProject)
        //                yield return new ProjectTreeImageProjectViewModel(node);
        //            else if (node.Value is ResourceFolder)
        //                yield return new ProjectTreeFolderViewModel(node);
        //            else if (node.Value is Palette)
        //                yield return new ProjectTreePaletteViewModel(node);
        //            else if (node.Value is DataFile)
        //                yield return new ProjectTreeDataFileViewModel(node);
        //            else if (node.Value is Arranger)
        //                yield return new ProjectTreeArrangerViewModel(node);
        //        }
        //    }
        //}

        private ProjectTreeNodeViewModel _selectedItem;
        public ProjectTreeNodeViewModel SelectedItem
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
                case ProjectTreeImageProjectViewModel project:
                    _events.PublishOnUIThread(new ActivateEditorEvent(project.Node.Value));
                    break;
                case ProjectTreePaletteViewModel pal:
                    _events.PublishOnUIThread(new ActivateEditorEvent(pal.Node.Value));
                    break;
                case ProjectTreeArrangerViewModel arranger:
                    _events.PublishOnUIThread(new ActivateEditorEvent(arranger.Node.Value));
                    break;
                case ProjectTreeDataFileViewModel file:
                    _events.PublishOnUIThread(new ActivateEditorEvent(file.Node.Value));
                    break;
                case ProjectTreeFolderViewModel folder:
                    _events.PublishOnUIThread(new ActivateEditorEvent(folder.Node.Value));
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(ActivateSelectedItem)} was called with a {nameof(SelectedItem)} of type {SelectedItem.GetType()}");
            }
        }

        private void CreateTree()
        {
            RootItems = new BindableCollection<ProjectTreeNodeViewModel>();

            if (_treeService.Tree is null)
                return;

            foreach (var node in _treeService.Tree.Children())
            {
                if (node.Value is ImageProject)
                    RootItems.Add(new ProjectTreeImageProjectViewModel(node));
                else if (node.Value is ResourceFolder)
                    RootItems.Add(new ProjectTreeFolderViewModel(node));
                else if (node.Value is Palette)
                    RootItems.Add(new ProjectTreePaletteViewModel(node));
                else if (node.Value is DataFile)
                    RootItems.Add(new ProjectTreeDataFileViewModel(node));
                else if (node.Value is Arranger)
                    RootItems.Add(new ProjectTreeArrangerViewModel(node));
            }
        }

        private void UnloadProject()
        {
            ProjectFileName = null;
            SelectedItem = null;
            _treeService.UnloadProject();

            CreateTree();
            NotifyOfPropertyChange(() => HasProject);
        }

        public void Handle(AddDataFileEvent message)
        {
            var dataFileName = _fileSelect.GetExistingDataFileNameByUser();

            if (dataFileName is object)
            {
                DataFile df = new DataFile(Path.GetFileName(dataFileName), dataFileName);
                var node = _treeService.AddResource(df);
                var nodeVm = new ProjectTreeDataFileViewModel(node);
                RootItems.Add(nodeVm);
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
                var nodeVm = new ProjectTreeDataFileViewModel(node);
                RootItems.Add(nodeVm);
                IsModified = true;
            }
        }

        public void DragOver(IDropInfo dropInfo)
        {
            var sourceNode = (dropInfo.Data as ProjectTreeNodeViewModel)?.Node;
            var targetNode = (dropInfo.TargetItem as ProjectTreeFolderViewModel)?.Node;

            if (_treeService.CanMoveNode(sourceNode, targetNode))
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            var sourceModel = dropInfo.Data as ProjectTreeNodeViewModel;
            var targetModel = dropInfo.TargetItem as ProjectTreeFolderViewModel;

            var sourceNode = sourceModel?.Node ?? null;
            var targetNode = targetModel?.Node ?? null;

            if (sourceNode is object && targetNode is object)
            {
                SelectedItem = null;
                _treeService.MoveNode(sourceNode, targetNode);
                IsModified = true;

                //sourceModel.Refresh();
                //targetModel.Refresh();

                NotifyOfPropertyChange(() => RootItems);
            }
        }

        public override void SaveChanges()
        {
            _treeService.SaveProject(ProjectFileName);
            IsModified = false;
        }

        public void NewProject(string newFileName)
        {
            _treeService.NewProject();
            ProjectFileName = newFileName;
            _treeService.SaveProject(ProjectFileName);

            foreach (var node in _treeService.Tree.EnumerateBreadthFirst())
            {

            }
            NotifyOfPropertyChange(() => RootItems);
        }

        public void OpenProject(string projectFileName)
        {
            _treeService.OpenProject(projectFileName);
            ProjectFileName = projectFileName;
            NotifyOfPropertyChange(() => RootItems);
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
            throw new NotImplementedException();
        }
    }
}
