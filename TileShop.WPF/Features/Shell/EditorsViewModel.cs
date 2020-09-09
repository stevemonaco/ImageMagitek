using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using ImageMagitek.Services;
using Jot;
using Stylet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using TileShop.WPF.Configuration;

namespace TileShop.WPF.ViewModels
{
    public class EditorsViewModel : PropertyChangedBase
    {
        private BindableCollection<ResourceEditorBaseViewModel> _editors = new BindableCollection<ResourceEditorBaseViewModel>();
        public BindableCollection<ResourceEditorBaseViewModel> Editors
        {
            get => _editors;
            set => SetAndNotify(ref _editors, value);
        }

        private ResourceEditorBaseViewModel _activeEditor;
        private readonly IWindowManager _windowManager;
        private readonly Tracker _tracker;
        private readonly ICodecService _codecService;
        private readonly IPaletteService _paletteService;
        private readonly AppSettings _settings;
        private readonly IEventAggregator _events;

        public ResourceEditorBaseViewModel ActiveEditor
        {
            get => _activeEditor;
            set => SetAndNotify(ref _activeEditor, value);
        }

        private readonly Dictionary<MessageBoxResult, string> messageBoxLabels = new Dictionary<MessageBoxResult, string>
        {
            { MessageBoxResult.Yes, "Save" }, { MessageBoxResult.No, "Discard" }, { MessageBoxResult.Cancel, "Cancel" }
        };

        public EditorsViewModel(AppSettings settings, IEventAggregator events, IWindowManager windowManager, Tracker tracker,
            ICodecService codecService, IPaletteService paletteService)
        {
            _settings = settings;
            _events = events;
            _windowManager = windowManager;
            _tracker = tracker;
            _codecService = codecService;
            _paletteService = paletteService;
        }

        public void CloseEditor(ResourceEditorBaseViewModel editor)
        {
            if (Editors.Contains(editor) && editor.IsModified)
            {
                var result = _windowManager.ShowMessageBox($"{editor.DisplayName} has been modified. Save changes?", "Save changes",
                    MessageBoxButton.YesNoCancel, buttonLabels: messageBoxLabels);

                if (result == MessageBoxResult.Yes)
                {
                    editor.SaveChanges();
                    Editors.Remove(editor);
                    ActiveEditor = Editors.FirstOrDefault();
                }
                else if (result == MessageBoxResult.No)
                {
                    editor.DiscardChanges();
                    Editors.Remove(editor);
                    ActiveEditor = Editors.FirstOrDefault();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }
            else if (Editors.Contains(editor))
            {
                Editors.Remove(editor);
                ActiveEditor = Editors.FirstOrDefault();
            }
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
                        newDocument = new ScatteredArrangerEditorViewModel(scatteredArranger, _events, _windowManager, _paletteService);
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

                        var newArranger = new SequentialArranger(8, 16, dataFile, _codecService.CodecFactory, codecName);
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
    }
}
