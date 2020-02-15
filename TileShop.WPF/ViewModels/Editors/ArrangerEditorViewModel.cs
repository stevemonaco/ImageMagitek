using ImageMagitek;
using System;
using Stylet;
using TileShop.WPF.Behaviors;
using TileShop.Shared.EventModels;
using TileShop.WPF.Helpers;
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

        protected ArrangerBitmapSource _arrangerSource;
        public ArrangerBitmapSource ArrangerSource
        {
            get => _arrangerSource;
            set
            {
                _arrangerSource = value;
                NotifyOfPropertyChange(() => ArrangerSource);
            }
        }

        public bool IsLinearLayout => _arranger?.Layout == ArrangerLayout.Single;
        public bool IsTiledLayout => _arranger?.Layout == ArrangerLayout.Tiled;

        public virtual bool CanShowGridlines => _arranger?.Layout == ArrangerLayout.Tiled;

        protected bool _showGridlines = false;
        public bool ShowGridlines
        {
            get => _showGridlines;
            set => SetAndNotify(ref _showGridlines, value);
        }

        protected BindableCollection<Gridline> _gridlines;
        public BindableCollection<Gridline> Gridlines
        {
            get => _gridlines;
            set => SetAndNotify(ref _gridlines, value);
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
                SetAndNotify(ref _zoom, value);
                CreateGridlines();
            }
        }

        public int MinZoom => 1;
        public int MaxZoom => 16;

        public bool CanChangeSnapMode => _arranger is object ? _arranger.Layout == ArrangerLayout.Tiled : false;

        protected EditMode _editMode = EditMode.ArrangeGraphics;
        public EditMode EditMode
        {
            get => _editMode;
            set => SetAndNotify(ref _editMode, value);
        }

        protected ArrangerSelector _selection;
        public ArrangerSelector Selection
        {
            get => _selection;
            set => SetAndNotify(ref _selection, value);
        }

        public virtual bool CanEditSelection => true;

        public virtual void Closing()
        {
            Console.WriteLine("lj;sadf");
        }

        public virtual void EditSelection()
        {
            ArrangerTransferModel transferModel;

            if (Selection.SnapMode == SnapMode.Element && _arranger.Layout == ArrangerLayout.Tiled)
            {
                // Clone a subsection of the arranger and show the full subarranger
                var arranger = _arranger.CloneArranger(Selection.SnappedX1, Selection.SnappedY1, Selection.SnappedWidth, Selection.SnappedHeight);
                transferModel = new ArrangerTransferModel(arranger, 0, 0, Selection.SnappedWidth, Selection.SnappedHeight);
            }
            else
            {
                // Clone the entire arranger and show a subsection of the cloned arranger
                var arranger = _arranger.CloneArranger();
                transferModel = new ArrangerTransferModel(arranger, Selection.SnappedX1, Selection.SnappedY1, Selection.SnappedWidth, Selection.SnappedHeight);
            }

            var editEvent = new EditArrangerPixelsEvent(transferModel);
            _events.PublishOnUIThread(editEvent);
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
                _events.PublishOnUIThread(notifyEvent);
            }
            else
            {
                var notifyMessage = $"{_arranger.Name}: ({(int)Math.Round(e.X / Zoom)}, {(int)Math.Round(e.Y / Zoom)})";
                var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
                _events.PublishOnUIThread(notifyEvent);
            }
        }

        public virtual void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            var notifyEvent = new NotifyStatusEvent("", NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThread(notifyEvent);
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
