using Caliburn.Micro;
using ImageMagitek;
using System;
using System.Collections.Generic;
using System.Text;
using TileShop.WPF.Behaviors;
using TileShop.Shared.EventModels;
using TileShop.WPF.Helpers;
using System.Threading;
using System.Threading.Tasks;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels
{
    public abstract class ArrangerEditorViewModel : ResourceEditorBaseViewModel, IMouseCaptureProxy
    {
        protected Arranger _arranger;
        protected ArrangerImage _arrangerImage = new ArrangerImage();
        protected IEventAggregator _events;

        protected ImageRgba32Source _arrangerSource;
        public ImageRgba32Source ArrangerSource
        {
            get => _arrangerSource;
            set
            {
                _arrangerSource = value;
                NotifyOfPropertyChange(() => ArrangerSource);
            }
        }

        public bool IsLinearLayout => _arranger?.Layout == ArrangerLayout.LinearArranger;
        public bool IsTiledLayout => _arranger?.Layout == ArrangerLayout.TiledArranger;

        public virtual bool CanShowGridlines => _arranger?.Layout == ArrangerLayout.TiledArranger;
        protected bool _showGridlines = false;
        public bool ShowGridlines
        {
            get => _showGridlines;
            set
            {
                _showGridlines = value;
                NotifyOfPropertyChange(() => ShowGridlines);
            }
        }

        protected BindableCollection<Gridline> _gridlines;
        public BindableCollection<Gridline> Gridlines
        {
            get => _gridlines;
            set
            {
                _gridlines = value;
                NotifyOfPropertyChange(() => Gridlines);
            }
        }

        public virtual event EventHandler Capture;
        public virtual event EventHandler Release;

        private int _zoom = 1;
        public int Zoom
        {
            get => _zoom;
            set
            {
                _zoom = value;
                NotifyOfPropertyChange(() => Zoom);
                CreateGridlines();
            }
        }

        public int MinZoom => 1;
        public int MaxZoom => 16;

        protected void CreateGridlines()
        {
            _gridlines = new BindableCollection<Gridline>();
            for (int x = 0; x < _arranger.ArrangerElementSize.Width; x++) // Vertical gridlines
            {
                var gridline = new Gridline(x * _arranger.ElementPixelSize.Width * Zoom + 1, 0,
                    x * _arranger.ElementPixelSize.Width * Zoom + 1, _arranger.ArrangerPixelSize.Height * Zoom);
                _gridlines.Add(gridline);
            }

            _gridlines.Add(new Gridline(_arranger.ArrangerPixelSize.Width * Zoom, 0,
                _arranger.ArrangerPixelSize.Width * Zoom, _arranger.ArrangerPixelSize.Height * Zoom));

            for (int y = 0; y < _arranger.ArrangerElementSize.Height; y++) // Horizontal gridlines
            {
                var gridline = new Gridline(0, y * _arranger.ElementPixelSize.Height * Zoom + 1,
                    _arranger.ArrangerPixelSize.Width * Zoom, y * _arranger.ElementPixelSize.Height * Zoom + 1);
                _gridlines.Add(gridline);
            }

            _gridlines.Add(new Gridline(0, _arranger.ArrangerPixelSize.Height * Zoom,
                _arranger.ArrangerPixelSize.Width * Zoom, _arranger.ArrangerPixelSize.Height * Zoom));

            NotifyOfPropertyChange(() => Gridlines);
        }

        public void ZoomIn() => Zoom = Math.Clamp(Zoom + 1, MinZoom, MaxZoom);
        public void ZoomOut() => Zoom = Math.Clamp(Zoom - 1, MinZoom, MaxZoom);
        public void ToggleGridlineVisibility() => ShowGridlines ^= true;

        public virtual void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            var notifyMessage = $"{_arranger.Name}: ({(int)e.X}, {(int)e.Y})";
            var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThreadAsync(notifyEvent);
        }

        public virtual void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            var notifyEvent = new NotifyStatusEvent("", NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThreadAsync(notifyEvent);
        }

        public virtual void OnMouseUp(object sender, MouseCaptureArgs e) { }
        public virtual void OnMouseDown(object sender, MouseCaptureArgs e) { }
    }
}
