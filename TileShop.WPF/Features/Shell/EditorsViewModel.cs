using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Services;
using TileShop.Shared.EventModels;
using TileShop.WPF.Configuration;
using Jot;
using Stylet;
using Serilog;

namespace TileShop.WPF.ViewModels
{
    public class EditorsViewModel : PropertyChangedBase, IHandle<EditArrangerPixelsEvent>
    {
        private readonly IWindowManager _windowManager;
        private readonly Tracker _tracker;
        private readonly ICodecService _codecService;
        private readonly IPaletteService _paletteService;
        private readonly IProjectService _projectService;
        private readonly AppSettings _settings;
        private readonly IEventAggregator _events;

        private BindableCollection<ResourceEditorBaseViewModel> _editors = new BindableCollection<ResourceEditorBaseViewModel>();
        public BindableCollection<ResourceEditorBaseViewModel> Editors
        {
            get => _editors;
            set => SetAndNotify(ref _editors, value);
        }

        private ResourceEditorBaseViewModel _activeEditor;
        public ResourceEditorBaseViewModel ActiveEditor
        {
            get => _activeEditor;
            set => SetAndNotify(ref _activeEditor, value);
        }

        private ArrangerEditorViewModel _activePixelEditor;
        public ArrangerEditorViewModel ActivePixelEditor
        {
            get => _activePixelEditor;
            set => SetAndNotify(ref _activePixelEditor, value);
        }

        private ShellViewModel _shell;
        public ShellViewModel Shell
        {
            get => _shell;
            set => SetAndNotify(ref _shell, value);
        }

        private readonly Dictionary<MessageBoxResult, string> _messageBoxLabels = new Dictionary<MessageBoxResult, string>
        {
            { MessageBoxResult.Yes, "Save" }, { MessageBoxResult.No, "Discard" }, { MessageBoxResult.Cancel, "Cancel" }
        };

        public EditorsViewModel(AppSettings settings, IEventAggregator events, IWindowManager windowManager, Tracker tracker,
            ICodecService codecService, IPaletteService paletteService, IProjectService projectService)
        {
            _settings = settings;
            _events = events;
            _windowManager = windowManager;
            _tracker = tracker;
            _codecService = codecService;
            _paletteService = paletteService;
            _projectService = projectService;

            _events.Subscribe(this);
        }

        public bool CloseEditor(ResourceEditorBaseViewModel editor)
        {
            if (editor == ActivePixelEditor)
                return ClosePixelEditor();

            if (editor.IsModified)
            {
                if (RequestSaveUserChanges(editor, true))
                {
                    var projectTree = _projectService.GetContainingProject(editor.Resource);
                    _projectService.SaveProject(projectTree)
                    .Switch(
                        success => { },
                        fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.FileLocation}: {fail.Reason}")
                    );
                }
                else
                {
                    return false;
                }
            }

            Editors.Remove(editor);
            ActiveEditor = Editors.FirstOrDefault();

            return true;
        }

        public bool ClosePixelEditor()
        {
            if (ActivePixelEditor is null)
                return true;

            if (ActivePixelEditor.IsModified)
            {
                if (!RequestSavePixelEditorChanges())
                    return false;
            }

            Shell.Tools.Remove(ActivePixelEditor);
            ActivePixelEditor = default;

            return true;
        }

        public void ActivateEditor(IProjectResource resource)
        {
            var openedDocument = Editors.FirstOrDefault(x => ReferenceEquals(x.Resource, resource));

            if (openedDocument is null)
            {
                ResourceEditorBaseViewModel newDocument;

                switch (resource)
                {
                    case Palette pal:
                        newDocument = new PaletteEditorViewModel(pal, _events);
                        break;
                    case ScatteredArranger scatteredArranger:
                        newDocument = new ScatteredArrangerEditorViewModel(scatteredArranger, _events, _windowManager, _paletteService, _projectService);
                        break;
                    case SequentialArranger sequentialArranger:
                        newDocument = new SequentialArrangerEditorViewModel(sequentialArranger, _events, _windowManager, _tracker, _codecService, _paletteService);
                        break;
                    case DataFile dataFile: // Always open a new SequentialArranger so users are able to view multiple sections of the same file at once
                        var extension = Path.GetExtension(dataFile.Location);
                        string codecName;
                        if (_settings.ExtensionCodecAssociations.ContainsKey(extension))
                            codecName = _settings.ExtensionCodecAssociations[extension];
                        else if (_settings.ExtensionCodecAssociations.ContainsKey("default"))
                            codecName = _settings.ExtensionCodecAssociations["default"];
                        else
                            codecName = "NES 1bpp";

                        var newArranger = new SequentialArranger(8, 16, dataFile, _paletteService.DefaultPalette, _codecService.CodecFactory, codecName);
                        newDocument = new SequentialArrangerEditorViewModel(newArranger, _events, _windowManager, _tracker, _codecService, _paletteService);
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
                    ActiveEditor = newDocument;
                }
            }
            else
                ActiveEditor = openedDocument;
        }

        public bool RequestSaveAllUserChanges()
        {
            try
            {
                var savedProjects = new HashSet<ProjectTree>();

                foreach (var editor in Editors.Where(x => x.IsModified))
                {
                    if (!RequestSaveUserChanges(editor, false))
                        return false;
                    savedProjects.Add(_projectService.GetContainingProject(editor.Resource));
                }

                foreach (var projectTree in savedProjects)
                {
                    _projectService.SaveProject(projectTree)
                     .Switch(
                         success => { },
                         fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.FileLocation}: {fail.Reason}")
                     );
                }

                return true;
            }
            catch (Exception ex)
            {
                _windowManager.ShowMessageBox(ex.Message);
                Log.Error(ex, "Unhandled exception");
                return false;
            }
        }

        /// <summary>
        /// Requests to the user if they want to save the specified editor and saves if necessary
        /// </summary>
        /// <param name="editor">Editor to save</param>
        /// <param name="saveTree">The project tree is also saved upon a Save confirmation</param>
        /// <returns>True if saved/discarded, false if cancelled</returns>
        public bool RequestSaveUserChanges(ResourceEditorBaseViewModel editor, bool saveTree)
        {
            if (editor.IsModified)
            {
                var result = _windowManager.ShowMessageBox($"'{editor.DisplayName}' has been modified and will be closed. Save changes?",
                    "Save changes", MessageBoxButton.YesNoCancel, buttonLabels: _messageBoxLabels);

                if (result == MessageBoxResult.Yes)
                {
                    editor.SaveChanges();
                    if (saveTree)
                    {
                        var projectTree = _projectService.GetContainingProject(editor.Resource);
                        _projectService.SaveProject(projectTree)
                         .Switch(
                             success => { },
                             fail => _windowManager.ShowMessageBox($"An error occurred while saving the project tree to {projectTree.FileLocation}: {fail.Reason}")
                         );
                    }
                        
                    return true;
                }
                if (result == MessageBoxResult.No)
                {
                    editor.DiscardChanges();
                    return true;
                }
                else if (result == MessageBoxResult.Cancel)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Requests to the user if they want to save the ActivePixelEditor
        /// </summary>
        /// <returns>True if saved/discarded, false if cancelled</returns>
        public bool RequestSavePixelEditorChanges()
        {
            if (ActivePixelEditor is null)
                return true;

            if (ActivePixelEditor.IsModified)
            {
                var result = _windowManager.ShowMessageBox($"'{ActivePixelEditor.DisplayName}' has been modified and will be closed. Save changes?",
                    "Save changes", MessageBoxButton.YesNoCancel, buttonLabels: _messageBoxLabels);

                if (result == MessageBoxResult.Yes)
                {
                    ActivePixelEditor.SaveChanges();
                    return true;
                }
                if (result == MessageBoxResult.No)
                {
                    ActivePixelEditor.DiscardChanges();
                    return true;
                }
                else if (result == MessageBoxResult.Cancel)
                    return false;
            }
            return true;
        }

        public void Handle(EditArrangerPixelsEvent message)
        {
            if (ActivePixelEditor?.IsModified is true)
            {
                if (!RequestSaveUserChanges(ActivePixelEditor, false))
                    return;
            }

            if (ActivePixelEditor is object)
                Shell.Tools.Remove(ActivePixelEditor);

            var model = message.ArrangerTransferModel;

            if (model.Arranger.ColorType == PixelColorType.Indexed)
            {
                var editor = new IndexedPixelEditorViewModel(model.Arranger, model.X, model.Y, model.Width, model.Height,
                    _events, _windowManager, _paletteService);

                ActivePixelEditor = editor;
                Shell.Tools.Add(ActivePixelEditor);
            }
            else if (model.Arranger.ColorType == PixelColorType.Direct)
            {

            }
        }
    }
}
