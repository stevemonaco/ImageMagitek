using Caliburn.Micro;
using ImageMagitek.Project;
using ImageMagitek;
using Monaco.PathTree;
using System;
using System.Collections.Generic;
using System.Text;
using ImageMagitek.Colors;
using TileShop.WPF.EventModels;
using System.Threading;
using System.Threading.Tasks;
using TileShop.WPF.Services;
using System.Windows.Threading;
using TileShop.Shared.Services;

namespace TileShop.WPF.ViewModels
{
    public class ProjectTreeViewModel : Screen, IHandle<OpenProjectEvent>
    {
        private IPathTree<IProjectResource> _tree;
        private IProjectTreeService _treeService;
        private IEventAggregator _events;
        private IFileSelectService _fileSelect;

        public ProjectTreeViewModel(IProjectTreeService treeService, IFileSelectService fileSelect, IEventAggregator events)
        {
            _treeService = treeService;
            _fileSelect = fileSelect;

            _events = events;
            _events.SubscribeOnUIThread(this);
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
            set
            {
                _selectedItem = value;
                NotifyOfPropertyChange(() => SelectedItem);
            }
        }

        public void ActivateSelectedItem()
        {
            if (SelectedItem is null)
                return;

            switch(SelectedItem)
            {
                case ProjectTreePaletteViewModel pal:
                    _events.PublishOnUIThreadAsync(new ActivateResourceEditorEvent(pal.Node.Value));
                    break;
                case ProjectTreeArrangerViewModel arranger:
                    _events.PublishOnUIThreadAsync(new ActivateResourceEditorEvent(arranger.Node.Value));
                    break;
                case ProjectTreeDataFileViewModel file:
                    _events.PublishOnUIThreadAsync(new ActivateResourceEditorEvent(file.Node.Value));
                    break;
                case ProjectTreeFolderViewModel folder:
                    _events.PublishOnUIThreadAsync(new ActivateResourceEditorEvent(folder.Node.Value));
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(ActivateSelectedItem)} was called with a {nameof(SelectedItem)} of type {SelectedItem.GetType()}");
            }
        }

        public Task HandleAsync(OpenProjectEvent message, CancellationToken cancellationToken)
        {
            //var projectFileName = await Dispatcher.CurrentDispatcher.InvokeAsync(() => _fileSelect.GetProjectByUser());

            var projectFileName = _fileSelect.GetProjectByUser();

            if (projectFileName is object)
            {
                _tree = _treeService.ReadProject(projectFileName);
                NotifyOfPropertyChange(() => RootItems);
            }

            return Task.CompletedTask;
        }
    }
}
