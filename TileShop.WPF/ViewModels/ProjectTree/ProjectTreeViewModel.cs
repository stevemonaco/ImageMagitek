using Caliburn.Micro;
using ImageMagitek.Project;
using ImageMagitek;
using Monaco.PathTree;
using System;
using System.Collections.Generic;
using System.Text;
using ImageMagitek.Colors;
using TileShop.Shared.EventModels;
using System.Threading;
using System.Threading.Tasks;
using TileShop.WPF.Services;
using System.Windows.Threading;
using TileShop.Shared.Services;
using System.IO;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeViewModel : Screen, IHandle<OpenProjectEvent>, IHandle<NewProjectEvent>,
        IHandle<AddDataFileEvent>, IHandle<SaveProjectEvent>, IHandle<CloseProjectEvent>
    {
        private IPathTree<IProjectResource> _tree;
        private IProjectTreeService _treeService;
        private IEventAggregator _events;
        private IFileSelectService _fileSelect;
        private IUserPromptService _promptService;

        public ProjectTreeViewModel(IProjectTreeService treeService, IFileSelectService fileSelect,
            IEventAggregator events, IUserPromptService promptService)
        {
            _treeService = treeService;
            _fileSelect = fileSelect;
            _promptService = promptService;

            _events = events;
            _events.SubscribeOnUIThread(this);
        }

        private string _activeProjectFileName;
        public string ActiveProjectFileName
        {
            get => _activeProjectFileName;
            set => Set(ref _activeProjectFileName, value);
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
            set => Set(ref _selectedItem, value);
        }

        public void ActivateSelectedItem()
        {
            if (SelectedItem is null)
                return;

            switch(SelectedItem)
            {
                case ProjectTreePaletteViewModel pal:
                    _events.PublishOnUIThreadAsync(new ActivateEditorEvent(pal.Node.Value));
                    break;
                case ProjectTreeArrangerViewModel arranger:
                    _events.PublishOnUIThreadAsync(new ActivateEditorEvent(arranger.Node.Value));
                    break;
                case ProjectTreeDataFileViewModel file:
                    _events.PublishOnUIThreadAsync(new ActivateEditorEvent(file.Node.Value));
                    break;
                case ProjectTreeFolderViewModel folder:
                    _events.PublishOnUIThreadAsync(new ActivateEditorEvent(folder.Node.Value));
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

        public bool CanSaveProject => ActiveProjectFileName is object;

        public bool HasProject => ActiveProjectFileName is object;

        public async Task HandleAsync(NewProjectEvent message, CancellationToken cancellationToken)
        {
            var projectFileName = _fileSelect.GetNewProjectFileNameByUser();

            if (projectFileName is object)
            {
                _tree = new PathTree<IProjectResource>();
                ActiveProjectFileName = projectFileName;
                SaveProject(ActiveProjectFileName);
                NotifyOfPropertyChange(() => RootItems);
                await _events.PublishOnUIThreadAsync(new ProjectLoadedEvent());
            }
        }

        public async Task HandleAsync(OpenProjectEvent message, CancellationToken cancellationToken)
        {
            var projectFileName = _fileSelect.GetProjectFileNameByUser();

            if (projectFileName is object)
            {
                _tree = _treeService.ReadProject(projectFileName);
                ActiveProjectFileName = projectFileName;
                NotifyOfPropertyChange(() => RootItems);
                await _events.PublishOnUIThreadAsync(new ProjectLoadedEvent(ActiveProjectFileName));
            }
        }

        public Task HandleAsync(AddDataFileEvent message, CancellationToken cancellationToken)
        {
            var dataFileName = _fileSelect.GetExistingDataFileNameByUser();

            if (dataFileName is object)
            {
                DataFile df = new DataFile(Path.GetFileName(dataFileName), dataFileName);
                _tree.Add(Path.GetFileName(dataFileName), df);
                NotifyOfPropertyChange(() => RootItems);
            }

            return Task.CompletedTask;
        }

        public async Task HandleAsync(SaveProjectEvent message, CancellationToken cancellationToken)
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
            await _events.PublishOnUIThreadAsync(new ProjectUnloadedEvent());
            await _events.PublishOnUIThreadAsync(new ProjectLoadedEvent(ActiveProjectFileName));
        }

        public Task HandleAsync(CloseProjectEvent message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
