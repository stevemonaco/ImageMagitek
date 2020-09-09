using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows;
using Stylet;
using AvalonDock;
using TileShop.Shared.EventModels;
using TileShop.WPF.Services;
using ImageMagitek;
using ImageMagitek.Services;
using TileShop.WPF.Configuration;
using Jot;

namespace TileShop.WPF.ViewModels
{
    public class ShellViewModel : Conductor<object>, IHandle<ShowToolWindowEvent>,
        IHandle<EditArrangerPixelsEvent>, IHandle<RequestApplicationExitEvent>
    {
        private readonly AppSettings _settings;
        private readonly IEventAggregator _events;
        private readonly IPaletteService _paletteService;
        private readonly IWindowManager _windowManager;
        private readonly IFileSelectService _fileSelect;
        private readonly IProjectService _projectService;

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

        private EditorsViewModel _editors;
        public EditorsViewModel Editors
        {
            get => _editors;
            set => SetAndNotify(ref _editors, value);
        }

        private BindableCollection<ToolViewModel> _tools = new BindableCollection<ToolViewModel>();
        public BindableCollection<ToolViewModel> Tools
        {
            get => _tools;
            set => SetAndNotify(ref _tools, value);
        }

        private ArrangerEditorViewModel _activePixelEditor;
        public ArrangerEditorViewModel ActivePixelEditor
        {
            get => _activePixelEditor;
            set => SetAndNotify(ref _activePixelEditor, value);
        }

        private readonly Dictionary<MessageBoxResult, string> messageBoxLabels = new Dictionary<MessageBoxResult, string> 
        { 
            { MessageBoxResult.Yes, "Save" }, { MessageBoxResult.No, "Discard" }, { MessageBoxResult.Cancel, "Cancel" } 
        };

        public ShellViewModel(AppSettings settings, IEventAggregator events, IWindowManager windowManager,
            IPaletteService paletteService, IFileSelectService fileSelect, IProjectService projectService,
            MenuViewModel activeMenu, ProjectTreeViewModel activeTree, StatusBarViewModel activeStatusBar, EditorsViewModel editors)
        {
            _settings = settings;
            _events = events;
            _events.Subscribe(this);
            _paletteService = paletteService;
            _windowManager = windowManager;
            _fileSelect = fileSelect;
            _projectService = projectService;
            Editors = editors;

            ActiveMenu = activeMenu;
            ActiveMenu.Shell = this;
            ActiveTree = activeTree;
            ActiveStatusBar = activeStatusBar;

            Tools.Add(activeTree);
        }

        public void Closing(CancelEventArgs e)
        {
            if (!RequestSaveAllUserChanges())
            {
                e.Cancel = true;
                return;
            }

            _projectService.CloseProjects();
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
                Editors.Editors.Remove(editor);
        }

        public void Handle(ShowToolWindowEvent message)
        {
            switch (message.ToolWindow)
            {
                case ToolWindow.ProjectExplorer:
                    if (!ActiveTree.IsVisible)
                    {
                        ActiveTree.IsVisible = true;
                        ActiveTree.IsActive = true;
                        ActiveTree.IsSelected = true;
                        Tools.Remove(ActiveTree);
                        Tools.Add(ActiveTree);
                    }
                    break;

                case ToolWindow.PixelEditor:
                    if (ActivePixelEditor is object)
                    {
                        ActivePixelEditor.IsActive = true;
                        ActivePixelEditor.IsSelected = true;
                        ActivePixelEditor.IsVisible = true;
                        Tools.Remove(ActivePixelEditor);
                        Tools.Add(ActivePixelEditor);
                    }
                    break;

                default:
                    throw new InvalidOperationException();
            }
        }

        private bool RequestSaveAllUserChanges()
        {
            try
            {
                if (ActivePixelEditor is object)
                {
                    if (!RequestSaveUserChanges(ActivePixelEditor))
                        return false;
                }

                ActivePixelEditor = null;

                foreach (var editor in Editors.Editors)
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
                    "Save changes", MessageBoxButton.YesNoCancel, buttonLabels: messageBoxLabels);

                if (result == MessageBoxResult.Yes)
                {
                    model.SaveChanges();
                    return true;
                }
                if (result == MessageBoxResult.No)
                {
                    model.DiscardChanges();
                    return true;
                }
                else if (result == MessageBoxResult.Cancel)
                    return false;
            }
            return true;
        }

        public void Handle(RequestApplicationExitEvent message)
        {
            if (RequestSaveAllUserChanges())
            {
                _projectService.CloseProjects();
                Environment.Exit(0);
            }
        }

        public void Handle(EditArrangerPixelsEvent message)
        {
            if (ActivePixelEditor?.IsModified is true)
            {
                if (!RequestSaveUserChanges(ActivePixelEditor))
                    return;
            }

            if (ActivePixelEditor is object)
                Tools.Remove(ActivePixelEditor);

            var model = message.ArrangerTransferModel;
            if (model.Arranger.ColorType == PixelColorType.Indexed)
            {
                var editor = new IndexedPixelEditorViewModel(model.Arranger, model.X, model.Y, model.Width, model.Height,
                    _events, _windowManager, _paletteService);

                ActivePixelEditor = editor;
                Tools.Add(ActivePixelEditor);
            }
            else if (model.Arranger.ColorType == PixelColorType.Direct)
            {

            }
        }
    }
}
