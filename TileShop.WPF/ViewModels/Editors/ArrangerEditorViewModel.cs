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
using TileShop.Shared.Models;
using TileShop.WPF.Imaging;

namespace TileShop.WPF.ViewModels
{
    public abstract class ArrangerEditorViewModel : ResourceEditorBaseViewModel, IMouseCaptureProxy
    {
        protected Arranger _arranger;
        protected IndexedImage _indexedImage;
        protected DirectImage _directImage;
        protected IEventAggregator _events;

        protected BitmapSourceBase _arrangerSource;
        public BitmapSourceBase ArrangerSource
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
            set => Set(ref _showGridlines, value);
        }

        protected BindableCollection<Gridline> _gridlines;
        public BindableCollection<Gridline> Gridlines
        {
            get => _gridlines;
            set => Set(ref _gridlines, value);
        }

#pragma warning disable CS0067
        // Unused events that are required to be present by the proxy
        public virtual event EventHandler Capture;
        public virtual event EventHandler Release;
#pragma warning restore CS0067

        protected int _zoom = 1;
        public int Zoom
        {
            get => _zoom;
            set
            {
                Set(ref _zoom, value);
                CreateGridlines();
            }
        }

        public int MinZoom => 1;
        public int MaxZoom => 16;

        public bool CanChangeSnapMode => _arranger is object ? _arranger.Layout == ArrangerLayout.TiledArranger : false;

        protected EditMode _editMode = EditMode.ArrangeGraphics;
        public EditMode EditMode
        {
            get => _editMode;
            set => Set(ref _editMode, value);
        }

        protected ArrangerSelector _selection;
        public ArrangerSelector Selection
        {
            get => _selection;
            set => Set(ref _selection, value);
        }

        public virtual bool CanEditSelection => true;

        public virtual void EditSelection()
        {
            ArrangerTransferModel transferModel;

            if (Selection.SnapMode == SnapMode.Element)
                transferModel = new ArrangerTransferModel(_arranger, Selection.SnappedX1, Selection.SnappedY1, Selection.SnappedWidth, Selection.SnappedHeight);
            else
                transferModel = new ArrangerTransferModel(_arranger, Selection.SnappedX1, Selection.SnappedY1, Selection.SnappedWidth, Selection.SnappedHeight);

            var editEvent = new EditArrangerPixelsEvent(transferModel);
            _events.PublishOnUIThreadAsync(editEvent);
        }

        public virtual void CancelSelection() => Selection?.CancelSelection();

        protected virtual void CreateGridlines()
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
            if (Selection.IsSelecting)
                Selection.UpdateSelection(e.X / Zoom, e.Y / Zoom);

            if (Selection.HasSelection)
            {
                string notifyMessage;
                if (Selection.SnapMode == SnapMode.Element)
                    notifyMessage = $"Element Selection: {Selection.SnappedWidth / _arranger.ElementPixelSize.Width} x {Selection.SnappedHeight / _arranger.ElementPixelSize.Height}" +
                        $" at ({Selection.SnappedX1 / _arranger.ElementPixelSize.Width}, {Selection.SnappedY1 / _arranger.ElementPixelSize.Height})";
                else
                    notifyMessage = $"Pixel Selection: {Selection.SnappedWidth} x {Selection.SnappedHeight}" +
                        $" at ({Selection.SnappedX1} x {Selection.SnappedY1})";
                var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
                _events.PublishOnUIThreadAsync(notifyEvent);
            }
            else
            {
                var notifyMessage = $"{_arranger.Name}: ({(int)Math.Round(e.X / Zoom)}, {(int)Math.Round(e.Y / Zoom)})";
                var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
                _events.PublishOnUIThreadAsync(notifyEvent);
            }
        }

        public virtual void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            var notifyEvent = new NotifyStatusEvent("", NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThreadAsync(notifyEvent);
        }

        public virtual void OnMouseUp(object sender, MouseCaptureArgs e)
        {
            Selection.StopSelection();
        }

        public virtual void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            if (!Selection.HasSelection && e.LeftButton)
            {
                Selection.StartSelection(e.X / Zoom, e.Y / Zoom);
            }
            if (Selection.HasSelection && e.LeftButton)
            {
                if (Selection.IsPointInSelection(e.X / Zoom, e.Y / Zoom)) // Start drag
                    return;
                else // New selection
                    Selection.StartSelection(e.X / Zoom, e.Y / Zoom);
            }
            if (Selection.HasSelection && e.RightButton)
            {
                Selection.CancelSelection();
            }
        }
    }
}
