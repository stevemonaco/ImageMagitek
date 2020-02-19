using System;
using System.Linq;
using System.Windows;
using Stylet;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using TileShop.Shared.EventModels;
using TileShop.Shared.Services;
using Xceed.Wpf.AvalonDock;

namespace TileShop.WPF.ViewModels
{
    public class ShellViewModel : Conductor<object>, IHandle<ActivateEditorEvent>, IHandle<ShowToolWindowEvent>
    {
        protected readonly IEventAggregator _events;
        protected readonly ICodecService _codecService;
        protected readonly IPaletteService _paletteService;
        private readonly IWindowManager _windowManager;

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

        private ToolViewModel _activeTool;
        public ToolViewModel ActiveTool
        {
            get { return _activeTool; }
            set => SetAndNotify(ref _activeTool, value);
        }

        private BindableCollection<ToolViewModel> _tools = new BindableCollection<ToolViewModel>();
        public BindableCollection<ToolViewModel> Tools
        {
            get => _tools;
            set => SetAndNotify(ref _tools, value);
        }

        private PixelEditorViewModel _activePixelEditor;
        public PixelEditorViewModel ActivePixelEditor
        {
            get => _activePixelEditor;
            set => SetAndNotify(ref _activePixelEditor, value);
        }

        public ShellViewModel(IEventAggregator events, IWindowManager windowManager, ICodecService codecService,
            IPaletteService paletteService, MenuViewModel activeMenu, ProjectTreeViewModel activeTree, 
            StatusBarViewModel activeStatusBar, PixelEditorViewModel activePixelEditor)
        {
            _events = events;
            _events.Subscribe(this);
            _codecService = codecService;
            _paletteService = paletteService;
            _windowManager = windowManager;

            ActiveMenu = activeMenu;
            ActiveTree = activeTree;
            ActiveStatusBar = activeStatusBar;
            ActivePixelEditor = activePixelEditor;

            Tools.Add(activeTree);
            Tools.Add(activePixelEditor);
        }

        public void Closing()
        {

        }

        public void DocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            var doc = e.Document.Content as ResourceEditorBaseViewModel;
            if (doc is null || !doc.IsModified)
                return;

            var result = _windowManager.ShowMessageBox($"{doc.DisplayName} has been changed. Save changes?", "Save changes", MessageBoxButton.YesNoCancel);

            if (result == MessageBoxResult.Yes)
                doc.SaveChanges();
            else if (result == MessageBoxResult.No)
                doc.DiscardChanges();
            else if (result == MessageBoxResult.Cancel)
                e.Cancel = true;
        }

        public void DocumentClosed(DocumentClosedEventArgs e)
        {
            var document = e.Document.Content as ResourceEditorBaseViewModel;

            if (document is object)
                Editors.Remove(document);
        }

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
                    ActiveTool = newDocument;
                }
            }
            else
                ActiveTool = openDocument;
        }

        public void Handle(ShowToolWindowEvent message)
        {
            switch (message.ToolWindow)
            {
                case ToolWindow.ProjectExplorer:
                    _activeTree.IsVisible = true;
                    _activeTree.IsActive = true;
                    _activeTree.IsSelected = true;
                    //Tools.Remove(_activeTree);
                    //Tools.Add(_activeTree);
                    break;

                case ToolWindow.PixelEditor:
                    _activePixelEditor.IsActive = true;
                    _activePixelEditor.IsSelected = true;
                    _activePixelEditor.IsVisible = true;
                    break;

                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
