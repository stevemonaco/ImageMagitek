﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;
using TileShop.Shared.EventModels;

namespace TileShop.WPF.ViewModels
{
    public class EditorHostViewModel : Screen, IHandle<ActivateEditorEvent>
    {
        protected IEventAggregator _events;

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

        public EditorHostViewModel(IEventAggregator events)
        {
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
