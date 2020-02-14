﻿using ImageMagitek.Project;
using ImageMagitek;
using Monaco.PathTree;
using System;
using System.Collections.Generic;
using Stylet;
using System.Text;
using ImageMagitek.Colors;
using TileShop.Shared.EventModels;
using System.Threading;
using System.Threading.Tasks;
using TileShop.WPF.Services;
using System.Windows.Threading;
using TileShop.Shared.Services;
using System.IO;
using TileShop.WPF.DialogModels;
using System.Linq;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeViewModel : Screen, IHandle<OpenProjectEvent>, IHandle<NewProjectEvent>,
        IHandle<AddDataFileEvent>, IHandle<SaveProjectEvent>, IHandle<CloseProjectEvent>, IHandle<AddPaletteEvent>
    {
        private IPathTree<IProjectResource> _tree;
        private IProjectTreeService _treeService;
        private IEventAggregator _events;
        private IFileSelectService _fileSelect;
        private IUserPromptService _promptService;
        private IDialogService _dialogService;

        public ProjectTreeViewModel(IProjectTreeService treeService, IFileSelectService fileSelect,
            IEventAggregator events, IUserPromptService promptService, IDialogService dialogService)
        {
            _treeService = treeService;
            _fileSelect = fileSelect;
            _promptService = promptService;
            _dialogService = dialogService;

            _events = events;
            _events.Subscribe(this);
        }

        private string _activeProjectFileName;
        public string ActiveProjectFileName
        {
            get => _activeProjectFileName;
            set => SetAndNotify(ref _activeProjectFileName, value);
        }

        public IEnumerable<Screen> RootItems
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

        private object _selectedItem;
        public object SelectedItem
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

        private void SaveProject(string projectFileName)
        {
            try
            {
                if (!_treeService.SaveProject(_tree, projectFileName))
                    _promptService.PromptUser($"An unspecified error occurred while saving the project tree to {projectFileName}", "Error", UserPromptChoices.Ok);
            }
            catch(Exception ex)
            {
                _promptService.PromptUser($"Unable to save project {projectFileName}\n{ex.Message}", "Error", UserPromptChoices.Ok);
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
                SaveProject(ActiveProjectFileName);
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
            string projectFileName;

            if (message.SaveAsNewProject)
            {
                projectFileName = _fileSelect.GetNewProjectFileNameByUser();

                if (projectFileName is null)
                    return;
            }
            else
                projectFileName = ActiveProjectFileName;

            SaveProject(projectFileName);
            ActiveProjectFileName = projectFileName;
            _events.PublishOnUIThread(new ProjectUnloadedEvent());
            _events.PublishOnUIThread(new ProjectLoadedEvent(ActiveProjectFileName));
        }

        public void Handle(CloseProjectEvent message)
        {
            _events.PublishOnUIThread(new ProjectClosingEvent());
            SaveProject(ActiveProjectFileName);
            Reset();
        }

        public void Handle(AddPaletteEvent message)
        {
            var model = new AddPaletteDialogModel();

            var dataFiles = _tree.EnumerateDepthFirst().Select(x => x.Value).OfType<DataFile>();
            model.DataFiles.AddRange(dataFiles);
            model.SelectedDataFile = model.DataFiles.FirstOrDefault();

            model.ColorModels.AddRange(Palette.GetColorModelNames());
            model.SelectedColorModel = model.ColorModels.First();
            model.Entries = 1;

            if (model.DataFiles.Count == 0)
            {
                _promptService.PromptUser("Project does not contain any data files to define a palette", "Project Error", UserPromptChoices.Ok);
                return;
            }
            
            if(_dialogService.ShowAddPaletteDialog(model))
            {
                var pal = new Palette(model.PaletteName, Palette.StringToColorModel(model.SelectedColorModel), model.FileOffset,
                    model.Entries, model.ZeroIndexTransparent, PaletteStorageSource.DataFile);
                pal.DataFile = model.SelectedDataFile;

                _tree.Add(pal.Name, pal);
                NotifyOfPropertyChange(() => RootItems);
            }

            return;
        }
    }
}
