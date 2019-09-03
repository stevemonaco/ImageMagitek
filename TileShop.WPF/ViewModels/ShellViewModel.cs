using Caliburn.Micro;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TileShop.Shared.EventModels;
using TileShop.Shared.Services;

namespace TileShop.WPF.ViewModels
{
    public class ShellViewModel : Conductor<object>, IHandle<ActivateEditorEvent>
    {
        protected IEventAggregator _events;
        protected ICodecService _codecService;

        private MenuViewModel _activeMenu;
        public MenuViewModel ActiveMenu
        {
            get =>_activeMenu;
            set
            {
                _activeMenu = value;
                NotifyOfPropertyChange(() => ActiveMenu);
            }
        }

        private ProjectTreeViewModel _activeTree;
        public ProjectTreeViewModel ActiveTree
        {
            get => _activeTree;
            set 
            { 
                _activeTree = value;
                NotifyOfPropertyChange(() => ActiveTree);
            }
        }

        private StatusBarViewModel _activeStatusBar;
        public StatusBarViewModel ActiveStatusBar
        {
            get => _activeStatusBar;
            set
            {
                _activeStatusBar = value;
                NotifyOfPropertyChange(() => ActiveStatusBar);
            }
        }

        private BindableCollection<EditorBaseViewModel> _editors = new BindableCollection<EditorBaseViewModel>();
        public BindableCollection<EditorBaseViewModel> Editors
        {
            get => _editors;
            set
            {
                _editors = value;
                NotifyOfPropertyChange(() => Editors);
            }
        }

        private EditorBaseViewModel _activeEditor;
        public EditorBaseViewModel ActiveEditor
        {
            get { return _activeEditor; }
            set
            {
                _activeEditor = value;
                NotifyOfPropertyChange(() => ActiveEditor);
            }
        }

        public ShellViewModel(IEventAggregator events, ICodecService codecService,
            MenuViewModel activeMenu, ProjectTreeViewModel activeTree, 
            StatusBarViewModel activeStatusBar)
        {
            _events = events;
            _events.SubscribeOnUIThread(this);
            _codecService = codecService;

            ActiveMenu = activeMenu;
            ActiveTree = activeTree;
            ActiveStatusBar = activeStatusBar;
        }

        public Task HandleAsync(ActivateEditorEvent message, CancellationToken cancellationToken)
        {
            var openDocument = Editors.FirstOrDefault(x => ReferenceEquals(x.Resource, message.Resource));

            if (openDocument is null)
            {
                EditorBaseViewModel newDocument;

                switch (message.Resource)
                {
                    case Palette pal:
                        newDocument = new PaletteEditorViewModel(pal);
                        break;
                    case ScatteredArranger scatteredArranger:
                        newDocument = new ScatteredArrangerEditorViewModel(scatteredArranger, _events);
                        break;
                    case SequentialArranger sequentialArranger:
                        newDocument = new SequentialArrangerEditorViewModel(sequentialArranger, _events, _codecService);
                        break;
                    case DataFile dataFile:
                        var newArranger = new SequentialArranger(8, 16, dataFile, _codecService.CodecFactory, "SNES 3bpp");
                        newDocument = new SequentialArrangerEditorViewModel(newArranger, _events, _codecService);
                        break;
                    case ResourceFolder resourceFolder:
                        newDocument = null;
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                if (newDocument is object)
                {
                    Editors.Add(newDocument);
                    ActiveEditor = newDocument;
                }
            }
            else
                ActiveEditor = openDocument;

            return Task.CompletedTask;
        }
    }
}
