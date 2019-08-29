using Caliburn.Micro;
using ImageMagitek;
using System;
using System.Collections.Generic;
using System.Text;
using TileShop.WPF.Behaviors;
using TileShop.Shared.EventModels;

namespace TileShop.WPF.ViewModels
{
    public class ArrangerEditorViewModel : EditorBaseViewModel, IMouseCaptureProxy
    {
        private Arranger _arranger;
        private IEventAggregator _events;

        public int DisplayHeight => _arranger.ArrangerPixelSize.Height * Zoom;
        public int DisplayWidth => _arranger.ArrangerPixelSize.Width * Zoom;

        public event EventHandler Capture;
        public event EventHandler Release;

        private int _zoom = 1;
        public int Zoom
        {
            get => _zoom;
            set
            {
                _zoom = value;
                NotifyOfPropertyChange(() => Zoom);
                NotifyOfPropertyChange(() => DisplayWidth);
                NotifyOfPropertyChange(() => DisplayHeight);
            }
        }

        public Uri Source => new Uri(@"F:\Projects\ImageMagitek\ff2.bmp", UriKind.Absolute);

        public ArrangerEditorViewModel(Arranger arranger, IEventAggregator events)
        {
            Resource = arranger;
            _arranger = arranger;
            _events = events;
        }

        public void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            var notifyMessage = $"{_arranger.Name}: ({(int)e.X}, {(int)e.Y})";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThreadAsync(notifyEvent);
        }

        public void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            var notifyEvent = new NotifyStatusEvent("", NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThreadAsync(notifyEvent);
        }

        public void OnMouseUp(object sender, MouseCaptureArgs e) { return; }
        public void OnMouseDown(object sender, MouseCaptureArgs e) { return; }
    }
}
