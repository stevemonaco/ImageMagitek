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
        protected IPaletteService _paletteService;

        private MenuViewModel _activeMenu;
        public MenuViewModel ActiveMenu
        {
            get =>_activeMenu;
            set => Set(ref _activeMenu, value);
        }

        private ProjectTreeViewModel _activeTree;
        public ProjectTreeViewModel ActiveTree
        {
            get => _activeTree;
            set => Set(ref _activeTree, value);
        }

        private StatusBarViewModel _activeStatusBar;
        public StatusBarViewModel ActiveStatusBar
        {
            get => _activeStatusBar;
            set => Set(ref _activeStatusBar, value);
        }

        private BindableCollection<ResourceEditorBaseViewModel> _editors = new BindableCollection<ResourceEditorBaseViewModel>();
        public BindableCollection<ResourceEditorBaseViewModel> Editors
        {
            get => _editors;
            set => Set(ref _editors, value);
        }

        private ResourceEditorBaseViewModel _activeEditor;
        public ResourceEditorBaseViewModel ActiveEditor
        {
            get { return _activeEditor; }
            set => Set(ref _activeEditor, value);
        }

        private PixelEditorViewModel _activePixelEditor;
        public PixelEditorViewModel ActivePixelEditor
        {
            get => _activePixelEditor;
            set => Set(ref _activePixelEditor, value);
        }

        public ShellViewModel(IEventAggregator events, ICodecService codecService, IPaletteService paletteService,
            MenuViewModel activeMenu, ProjectTreeViewModel activeTree, 
            StatusBarViewModel activeStatusBar, PixelEditorViewModel activePixelEditor)
        {
            _events = events;
            _events.SubscribeOnUIThread(this);
            _codecService = codecService;
            _paletteService = paletteService;

            ActiveMenu = activeMenu;
            ActiveTree = activeTree;
            ActiveStatusBar = activeStatusBar;
            ActivePixelEditor = activePixelEditor;
        }

        public Task HandleAsync(ActivateEditorEvent message, CancellationToken cancellationToken)
        {
            var openDocument = Editors.FirstOrDefault(x => ReferenceEquals(x.Resource, message.Resource));

            if (openDocument is null)
            {
                ResourceEditorBaseViewModel newDocument;

                switch (message.Resource)
                {
                    case Palette pal:
                        newDocument = new PaletteEditorViewModel(pal, _events);
                        break;
                    case ScatteredArranger scatteredArranger:
                        newDocument = new ScatteredArrangerEditorViewModel(scatteredArranger, _events, _paletteService);
                        break;
                    case SequentialArranger sequentialArranger:
                        newDocument = new SequentialArrangerEditorViewModel(sequentialArranger, _events, _codecService, _paletteService);
                        break;
                    case DataFile dataFile:
                        var newArranger = new SequentialArranger(8, 16, dataFile, _codecService.CodecFactory, "SNES 3bpp");
                        newDocument = new SequentialArrangerEditorViewModel(newArranger, _events, _codecService, _paletteService);
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
