using System;
using System.Linq;
using Stylet;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
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
            set => SetAndNotify(ref _activeMenu, value);
        }

        private ProjectTreeViewModel _activeTree;
        public ProjectTreeViewModel ActiveTree
        {
            get => _activeTree;
            set => SetAndNotify(ref _activeTree, value);
        }

        private StatusBarViewModel _activeStatusBar;
        public StatusBarViewModel ActiveStatusBar
        {
            get => _activeStatusBar;
            set => SetAndNotify(ref _activeStatusBar, value);
        }

        private BindableCollection<ResourceEditorBaseViewModel> _editors = new BindableCollection<ResourceEditorBaseViewModel>();
        public BindableCollection<ResourceEditorBaseViewModel> Editors
        {
            get => _editors;
            set => SetAndNotify(ref _editors, value);
        }

        private ResourceEditorBaseViewModel _activeEditor;
        public ResourceEditorBaseViewModel ActiveEditor
        {
            get { return _activeEditor; }
            set => SetAndNotify(ref _activeEditor, value);
        }

        private PixelEditorViewModel _activePixelEditor;
        public PixelEditorViewModel ActivePixelEditor
        {
            get => _activePixelEditor;
            set => SetAndNotify(ref _activePixelEditor, value);
        }

        public ShellViewModel(IEventAggregator events, ICodecService codecService, IPaletteService paletteService,
            MenuViewModel activeMenu, ProjectTreeViewModel activeTree, 
            StatusBarViewModel activeStatusBar, PixelEditorViewModel activePixelEditor)
        {
            _events = events;
            _events.Subscribe(this);
            _codecService = codecService;
            _paletteService = paletteService;

            ActiveMenu = activeMenu;
            ActiveTree = activeTree;
            ActiveStatusBar = activeStatusBar;
            ActivePixelEditor = activePixelEditor;
        }

        public void Closing() { }

        public void Handle(ActivateEditorEvent message)
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
        }
    }
}
