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
    public class ProjectTreeViewModel : ToolViewModel, IDropTarget, IHandle<OpenProjectEvent>, IHandle<NewProjectEvent>,
        IHandle<AddDataFileEvent>, IHandle<SaveProjectEvent>, IHandle<CloseProjectEvent>, IHandle<AddPaletteEvent>
    {
        private IPathTree<IProjectResource> _tree;
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

        private string _activeProjectFileName;
        public string ActiveProjectFileName
        {
            get => _activeProjectFileName;
            set => SetAndNotify(ref _activeProjectFileName, value);
        }

        public IEnumerable<ProjectTreeNodeViewModel> RootItems
        {
            get
            {
                if (_tree is null)
                    yield break;

                foreach (var node in _tree.Children())
                {
                    if (node.Value is ResourceFolder)
                        yield return new ProjectTreeFolderViewModel(node);
                    else if (node.Value is Palette)
                        yield return new ProjectTreePaletteViewModel(node);
                    else if (node.Value is DataFile)
                        yield return new ProjectTreeDataFileViewModel(node);
                    else if (node.Value is Arranger)
                        yield return new ProjectTreeArrangerViewModel(node);
                }
            }
        }

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

        public bool HasProject => ActiveProjectFileName is object;

        private void Reset()
        {
            ActiveProjectFileName = null;
            SelectedItem = null;
            _tree = null;

            NotifyOfPropertyChange(() => RootItems);
            NotifyOfPropertyChange(() => HasProject);
        }

        public void Handle(NewProjectEvent message)
        {
            var projectFileName = _fileSelect.GetNewProjectFileNameByUser();

            if (projectFileName is object)
            {
                _tree = new PathTree<IProjectResource>();
                ActiveProjectFileName = projectFileName;
                TrySaveProject(ActiveProjectFileName);
                NotifyOfPropertyChange(() => RootItems);
                _events.PublishOnUIThread(new ProjectLoadedEvent());
            }
        }

        public void Handle(OpenProjectEvent message)
        {
            var projectFileName = _fileSelect.GetProjectFileNameByUser();

            if (projectFileName is object)
            {
                _tree = _treeService.ReadProject(projectFileName);
                ActiveProjectFileName = projectFileName;
                NotifyOfPropertyChange(() => RootItems);
                _events.PublishOnUIThread(new ProjectLoadedEvent(ActiveProjectFileName));
            }
        }

        public void Handle(AddDataFileEvent message)
        {
            var dataFileName = _fileSelect.GetExistingDataFileNameByUser();

            if (dataFileName is object)
            {
                DataFile df = new DataFile(Path.GetFileName(dataFileName), dataFileName);
                _tree.Add(Path.GetFileName(dataFileName), df);
                NotifyOfPropertyChange(() => RootItems);
            }
        }

        public void Handle(SaveProjectEvent message)
        {
            string projectFileName = ActiveProjectFileName;

            if (message.SaveAsNewProject)
            {
                projectFileName = _fileSelect.GetNewProjectFileNameByUser();

                if (projectFileName is null)
                    return;
            }

            if (TrySaveProject(projectFileName))
            {
                ActiveProjectFileName = projectFileName;
                _events.PublishOnUIThread(new ProjectUnloadedEvent());
                _events.PublishOnUIThread(new ProjectLoadedEvent(ActiveProjectFileName));
            }
        }

        public void Handle(CloseProjectEvent message)
        {
            _events.PublishOnUIThread(new ProjectClosingEvent());

            if (IsModified)
                TrySaveProject(ActiveProjectFileName);
            Reset();
        }

        public void Handle(AddPaletteEvent message)
        {
            var model = new AddPaletteViewModel();

            var dataFiles = _tree.EnumerateDepthFirst().Select(x => x.Value).OfType<DataFile>();
            model.DataFiles.AddRange(dataFiles);
            model.SelectedDataFile = model.DataFiles.FirstOrDefault();

            model.ColorModels.AddRange(Palette.GetColorModelNames());
            model.SelectedColorModel = model.ColorModels.First();
            model.Entries = 1;

            if (model.DataFiles.Count == 0)
            {
                _windowManager.ShowMessageBox("Project does not contain any data files to define a palette", "Project Error");
                return;
            }

            if(_windowManager.ShowDialog(model) is true)
            {
                var pal = new Palette(model.PaletteName, Palette.StringToColorModel(model.SelectedColorModel), model.FileOffset,
                    model.Entries, model.ZeroIndexTransparent, PaletteStorageSource.DataFile);
                pal.DataFile = model.SelectedDataFile;

                _tree.Add(pal.Name, pal);
                IsModified = true;
                NotifyOfPropertyChange(() => RootItems);
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
            _treeService.SaveProject(_tree, ActiveProjectFileName);
        }

        private bool TrySaveProject(string projectFileName)
        {
            try
            {
                if (!_treeService.SaveProject(_tree, projectFileName))
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
