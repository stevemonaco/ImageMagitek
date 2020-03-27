using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Stylet;
using AvalonDock;
using TileShop.Shared.EventModels;
using TileShop.Shared.Services;
using TileShop.WPF.Services;
using TileShop.WPF.EventModels;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;

namespace TileShop.WPF.ViewModels
{
    public class ShellViewModel : Conductor<object>, IHandle<ActivateEditorEvent>, IHandle<ShowToolWindowEvent>,
        IHandle<OpenProjectEvent>, IHandle<NewProjectEvent>, IHandle<SaveProjectEvent>, IHandle<CloseProjectEvent>,
        IHandle<RequestRemoveTreeNodeEvent>
    {
        protected readonly IEventAggregator _events;
        protected readonly ICodecService _codecService;
        protected readonly IPaletteService _paletteService;
        private readonly IWindowManager _windowManager;
        private readonly IFileSelectService _fileSelect;
        private readonly IProjectTreeService _treeService;

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
            get => _activeTool;
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

        private Dictionary<MessageBoxResult, string> messageBoxLabels = new Dictionary<MessageBoxResult, string> 
        { 
            { MessageBoxResult.Yes, "Save" }, { MessageBoxResult.No, "Discard" }, { MessageBoxResult.Cancel, "Cancel" } 
        };

        public ShellViewModel(IEventAggregator events, IWindowManager windowManager, ICodecService codecService,
            IPaletteService paletteService, IFileSelectService fileSelect, IProjectTreeService treeService, MenuViewModel activeMenu,
            ProjectTreeViewModel activeTree, StatusBarViewModel activeStatusBar, PixelEditorViewModel activePixelEditor)
        {
            _events = events;
            _events.Subscribe(this);
            _codecService = codecService;
            _paletteService = paletteService;
            _windowManager = windowManager;
            _fileSelect = fileSelect;
            _treeService = treeService;

            ActiveMenu = activeMenu;
            ActiveTree = activeTree;
            ActiveStatusBar = activeStatusBar;
            ActivePixelEditor = activePixelEditor;

            Tools.Add(activeTree);
            Tools.Add(activePixelEditor);
        }

        public void Closing(CancelEventArgs e)
        {
            if (!RequestSaveAllUserChanges())
                e.Cancel = true;
        }

        public void DocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            if (!(e.Document.Content is ResourceEditorBaseViewModel doc) || !doc.IsModified)
                return;

            var result = _windowManager.ShowMessageBox($"{doc.DisplayName} has been modified. Save changes?", "Save changes",
                MessageBoxButton.YesNoCancel, buttonLabels: messageBoxLabels);

            if (result == MessageBoxResult.Yes)
                doc.SaveChanges();
            else if (result == MessageBoxResult.No)
                doc.DiscardChanges();
            else if (result == MessageBoxResult.Cancel)
                e.Cancel = true;
        }

        public void DocumentClosed(DocumentClosedEventArgs e)
        {
            if (e.Document.Content is ResourceEditorBaseViewModel editor)
                Editors.Remove(editor);
        }

        public void Handle(ActivateEditorEvent message)
        {
            var openedDocument = Editors.FirstOrDefault(x => ReferenceEquals(x.Resource, message.Resource));

            if (openedDocument is null)
            {
                ResourceEditorBaseViewModel newDocument;

                switch (message.Resource)
                {
                    case Palette pal:
                        newDocument = new PaletteEditorViewModel(pal, _events);
                        break;
                    case ScatteredArranger scatteredArranger:
                        newDocument = new ScatteredArrangerEditorViewModel(scatteredArranger, _events, _windowManager, _paletteService);
                        break;
                    case SequentialArranger sequentialArranger:
                        newDocument = new SequentialArrangerEditorViewModel(sequentialArranger, _events, _windowManager, _codecService, _paletteService);
                        break;
                    case DataFile dataFile: // Always open a new SequentialArranger so users are able to view multiple sections of the same file at once
                        var newArranger = new SequentialArranger(8, 16, dataFile, _codecService.CodecFactory, "SNES 3bpp");
                        newDocument = new SequentialArrangerEditorViewModel(newArranger, _events, _windowManager, _codecService, _paletteService);
                        break;
                    case ResourceFolder resourceFolder:
                        newDocument = null;
                        break;
                    case ImageProject project:
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
                ActiveTool = openedDocument;
        }

        public void Handle(ShowToolWindowEvent message)
        {
            switch (message.ToolWindow)
            {
                case ToolWindow.ProjectExplorer:
                    ActiveTree.IsVisible = true;
                    ActiveTree.IsActive = true;
                    ActiveTree.IsSelected = true;
                    Tools.Remove(ActiveTree);
                    Tools.Add(ActiveTree);
                    break;

                case ToolWindow.PixelEditor:
                    ActivePixelEditor.IsActive = true;
                    ActivePixelEditor.IsSelected = true;
                    ActivePixelEditor.IsVisible = true;
                    Tools.Remove(ActivePixelEditor);
                    Tools.Add(ActivePixelEditor);
                    break;

                default:
                    throw new InvalidOperationException();
            }
        }

        public void Handle(NewProjectEvent message)
        {
            if (!RequestSaveAllUserChanges())
                return;

            var projectFileName = _fileSelect.GetNewProjectFileNameByUser();

            try
            {
                if (projectFileName is object)
                {
                    Editors.Clear();
                    ActivePixelEditor.Reset();

                    ActiveTree.NewProject(projectFileName);
                    _events.PublishOnUIThread(new ProjectLoadedEvent());
                }
            }
            catch (Exception ex)
            {
                _windowManager.ShowMessageBox($"Unable to create new project at location '{projectFileName}'\n{ex.Message}\n{ex.StackTrace}");
                // TODO: Log
            }

        }

        public void Handle(OpenProjectEvent message)
        {
            if (!RequestSaveAllUserChanges())
                return;

            var projectFileName = _fileSelect.GetProjectFileNameByUser();

            try
            {
                if (projectFileName is object)
                {
                    Editors.Clear();
                    ActivePixelEditor.Reset();

                    ActiveTree.OpenProject(projectFileName);
                    _events.PublishOnUIThread(new ProjectLoadedEvent(ActiveTree.ProjectFileName));
                }
            }
            catch (Exception ex)
            {
                _windowManager.ShowMessageBox($"Unable to open project at location '{projectFileName}'\n{ex.Message}\n{ex.StackTrace}");
                // TODO: Log
            }
        }

        public void Handle(SaveProjectEvent message)
        {
            if (message.SaveAsNewProject)
            {
                var projectFileName = _fileSelect.GetNewProjectFileNameByUser();

                if (projectFileName is null)
                    return;

                ActiveTree.ProjectFileName = projectFileName;
            }

            ActiveTree.SaveChanges();
        }

        public void Handle(CloseProjectEvent message)
        {
            if (!RequestSaveAllUserChanges())
                return;

            Editors.Clear();
            ActivePixelEditor.Reset();
            ActiveTree.CloseProject();
        }

        private bool RequestSaveAllUserChanges()
        {
            try
            {
                if (!RequestSaveUserChanges(ActivePixelEditor))
                    return false;

                ActivePixelEditor.Reset();

                foreach (var editor in Editors)
                {
                    if (!RequestSaveUserChanges(editor))
                        return false;
                }

                return RequestSaveUserChanges(ActiveTree);
            }
            catch (Exception ex)
            {
                _windowManager.ShowMessageBox(ex.Message);
                // TODO: Log full exception here
                return false;
            }
        }

        private bool RequestSaveUserChanges(ToolViewModel model)
        {
            if (model.IsModified)
            {
                var result = _windowManager.ShowMessageBox($"'{model.DisplayName}' has been modified and will be closed. Save changes?",
                    "Save changes", System.Windows.MessageBoxButton.YesNoCancel, buttonLabels: messageBoxLabels);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    model.SaveChanges();
                    return true;
                }
                if (result == System.Windows.MessageBoxResult.No)
                {
                    model.DiscardChanges();
                    return true;
                }
                else if (result == System.Windows.MessageBoxResult.Cancel)
                    return false;
            }
            return true;
        }

        public void Handle(RequestRemoveTreeNodeEvent message)
        {
            var changeVm = _treeService.GetResourceRemovalChanges(ActiveTree.ProjectRoot.First(), message.TreeNode);

            bool? result;
            result = _windowManager.ShowDialog(changeVm);

            if (result is true)
            {
                var modifiedEditors = Editors.Where(x => x.IsModified).Concat(Tools.OfType<PixelEditorViewModel>().Where(x => x.IsModified));

                if (modifiedEditors.Any())
                {
                    var boxResult = _windowManager.ShowMessageBox("The project contains modified items which must be saved or discarded before removing any items", "Save changes",
                        MessageBoxButton.YesNoCancel, buttonLabels: messageBoxLabels);

                    if (boxResult == MessageBoxResult.Yes)
                    {
                        foreach (var editor in modifiedEditors)
                            editor.SaveChanges();

                        ActiveTree.SaveChanges();
                    }
                    else if (boxResult == MessageBoxResult.No)
                    {
                        foreach (var editor in modifiedEditors)
                            editor.DiscardChanges();

                        ActiveTree.SaveChanges();
                    }
                    else if (boxResult == MessageBoxResult.Cancel)
                        return;
                }

                ActivePixelEditor.Reset();
                Editors.Clear();

                ActiveTree.ApplyRemovalChanges(changeVm);
                ActiveTree.SaveChanges();
            }
        }
    }
}
