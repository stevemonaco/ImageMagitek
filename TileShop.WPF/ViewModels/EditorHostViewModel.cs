using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using TileShop.Shared.EventModels;
using TileShop.Shared.Services;

namespace TileShop.WPF.ViewModels
{
    public class EditorHostViewModel : Screen, IHandle<ActivateEditorEvent>
    {
        protected IEventAggregator _events;
        protected ICodecService _codecService;

        private BindableCollection<EditorBaseViewModel> _documents = new BindableCollection<EditorBaseViewModel>();
        public BindableCollection<EditorBaseViewModel> Documents
        {
            get => _documents;
            set
            {
                _documents = value;
                NotifyOfPropertyChange(() => Documents);
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

        public EditorHostViewModel(IEventAggregator events, ICodecService codecService)
        {
            _codecService = codecService;
            _events = events;
            _events.SubscribeOnUIThread(this);
        }

        public Task HandleAsync(ActivateEditorEvent message, CancellationToken cancellationToken)
        {
            var openDocument = Documents.FirstOrDefault(x => ReferenceEquals(x.Resource, message.Resource));

            if (openDocument is null)
            {
                EditorBaseViewModel newDocument;

                switch(message.Resource)
                {
                    case Palette pal:
                        newDocument = new PaletteEditorViewModel(pal);
                        break;
                    case ScatteredArranger scatteredArranger:
                        newDocument = new ScatteredArrangerEditorViewModel(scatteredArranger, _events);
                        break;
                    case SequentialArranger sequentialArranger:
                        newDocument = new SequentialArrangerEditorViewModel(sequentialArranger, _events);
                        break;
                    case DataFile dataFile:
                        var newArranger = new SequentialArranger(8, 16, dataFile, _codecService.CodecFactory, "SNES 3bpp");
                        newDocument = new SequentialArrangerEditorViewModel(newArranger, _events);
                        break;
                    case ResourceFolder resourceFolder:
                        newDocument = null;
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                if(newDocument is object)
                {
                    Documents.Add(newDocument);
                    ActiveEditor = newDocument;
                }
            }
            else
                ActiveEditor = openDocument;

            return Task.CompletedTask;
        }
    }
}
