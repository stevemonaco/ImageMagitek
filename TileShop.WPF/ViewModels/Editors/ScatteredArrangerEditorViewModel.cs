using Caliburn.Micro;
using ImageMagitek;
using System;
using TileShop.WPF.Behaviors;
using TileShop.Shared.EventModels;
using TileShop.WPF.Helpers;
using TileShop.WPF.Models;

namespace TileShop.WPF.ViewModels
{
    public enum SnapMode { Element, Pixel }
    public enum EditMode { ArrangeGraphics, ModifyGraphics }

    public class ScatteredArrangerEditorViewModel : EditorBaseViewModel, IMouseCaptureProxy
    {
        private Arranger _arranger;
        private ArrangerImage _arrangerImage = new ArrangerImage();
        private IEventAggregator _events;

        private ImageRgba32Source _arrangerSource;
        public ImageRgba32Source ArrangerSource
        {
            get => _arrangerSource;
            set
            {
                _arrangerSource = value;
                NotifyOfPropertyChange(() => ArrangerSource);
            }
        }

        public int DisplayHeight => _arranger.ArrangerPixelSize.Height * Zoom + 2;
        public int DisplayWidth => _arranger.ArrangerPixelSize.Width * Zoom + 2;

        private bool _showGridlines = false;
        public bool ShowGridlines
        {
            get => _showGridlines;
            set
            {
                _showGridlines = value;
                NotifyOfPropertyChange(() => ShowGridlines);
            }
        }

        public bool CanShowGridlines => _arranger.Layout == ArrangerLayout.TiledArranger;
        public bool CanChangeSnapMode => _arranger.Layout == ArrangerLayout.TiledArranger;

        private BindableCollection<Gridline> _gridlines;
        public BindableCollection<Gridline> Gridlines
        {
            get => _gridlines;
            set
            {
                _gridlines = value;
                NotifyOfPropertyChange(() => Gridlines);
            }
        }

        private EditMode _editMode = EditMode.ArrangeGraphics;
        public EditMode EditMode
        {
            get => _editMode;
            set
            {
                _editMode = value;
                NotifyOfPropertyChange(() => EditMode);
            }
        }

        private SnapMode _snapMode = SnapMode.Element;
        public SnapMode SnapMode
        {
            get => _snapMode;
            set
            {
                _snapMode = value;
                NotifyOfPropertyChange(() => SnapMode);
            }
        }

        public event EventHandler Capture;
        public event EventHandler Release;

        private int _zoom = 1;
        public int Zoom
        {
            get => _zoom;
            set
            {
                _zoom = value;
                CreateGridlines();
                NotifyOfPropertyChange(() => Zoom);
                NotifyOfPropertyChange(() => DisplayWidth);
                NotifyOfPropertyChange(() => DisplayHeight);
            }
        }

        public ScatteredArrangerEditorViewModel(Arranger arranger, IEventAggregator events)
        {
            Resource = arranger;
            _arranger = arranger;
            _events = events;

            _arrangerImage.Render(_arranger);
            ArrangerSource = new ImageRgba32Source(_arrangerImage.Image);
            CreateGridlines();
        }

        public void ToggleGridlines()
        {

        }

        private void CreateGridlines()
        {
            _gridlines = new BindableCollection<Gridline>();
            for(int x = 0; x < _arranger.ArrangerElementSize.Width; x++) // Vertical gridlines
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
