using System;
using System.Windows;
using System.Linq;
using Stylet;
using TileShop.WPF.Behaviors;
using TileShop.Shared.EventModels;
using TileShop.WPF.Models;
using TileShop.Shared.Models;
using TileShop.WPF.Imaging;
using GongSolutions.Wpf.DragDrop;
using ImageMagitek.Services;
using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Image;

namespace TileShop.WPF.ViewModels
{
    public enum EditMode { ArrangeGraphics, ModifyGraphics }

    public abstract class ArrangerEditorViewModel : ResourceEditorBaseViewModel, IMouseCaptureProxy, IDropTarget, IDragSource
    {
        protected Arranger _workingArranger;
        protected IndexedImage _indexedImage;
        protected DirectImage _directImage;

        protected IEventAggregator _events;
        protected IPaletteService _paletteService;
        protected IWindowManager _windowManager;

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

        public bool IsSingleLayout => _workingArranger?.Layout == ArrangerLayout.Single;
        public bool IsTiledLayout => _workingArranger?.Layout == ArrangerLayout.Tiled;

        public virtual bool CanShowGridlines => _workingArranger?.Layout == ArrangerLayout.Tiled;

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
        // Unused events that are required to be present for IMouseCaptureProxy
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
        public int MaxZoom { get; protected set; } = 16;

        public bool CanChangeSnapMode => _workingArranger is object ? _workingArranger.Layout == ArrangerLayout.Tiled : false;

        protected EditMode _editMode = EditMode.ArrangeGraphics;
        public EditMode EditMode
        {
            get => _editMode;
            set => SetAndNotify(ref _editMode, value);
        }

        public virtual bool CanEditSelection
        {
            get
            {
                if (Overlay.State == OverlayState.Selected)
                {
                    var rect = Overlay.SelectionRect;
                    if (rect.SnappedWidth == 0 || rect.SnappedHeight == 0)
                        return false;

                    var elems = _workingArranger.EnumerateElementsByPixel(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
                    return !elems.Any(x => x.DataFile is null || x.Codec is BlankIndexedCodec || x.Codec is BlankDirectCodec);
                }
                    
                return false;
            }
        }

        private SnapMode _snapMode = SnapMode.Element;
        public SnapMode SnapMode
        {
            get => _snapMode;
            set
            {
                SetAndNotify(ref _snapMode, value);
                Overlay.UpdateSnapMode(SnapMode);
            }
        }

        private ArrangerOverlay _overlay = new ArrangerOverlay();
        public ArrangerOverlay Overlay
        {
            get => _overlay;
            set => SetAndNotify(ref _overlay, value);
        }

        private bool _canPasteElements;
        public bool CanPasteElements
        {
            get => _canPasteElements;
            set => SetAndNotify(ref _canPasteElements, value);
        }

        private bool _canPastePixels;
        public bool CanPastePixels
        {
            get => _canPastePixels;
            set => SetAndNotify(ref _canPastePixels, value);
        }

        protected ArrangerTransferModel _arrangerTransfer;
        public ArrangerTransferModel ArrangerTransfer
        {
            get => _arrangerTransfer;
            set => SetAndNotify(ref _arrangerTransfer, value);
        }

        public ArrangerEditorViewModel(IEventAggregator events, IWindowManager windowManager, IPaletteService paletteService) 
        {
            _events = events;
            _events.Subscribe(this);
            _windowManager = windowManager;
            _paletteService = paletteService;
        }

        protected abstract void Render();

        public virtual void Closing() { }

        public virtual void RequestEditSelection()
        {
            if (CanEditSelection)
                EditSelection();
        }

        public virtual void EditSelection()
        {
            if (!CanEditSelection)
                return;

            ArrangerTransferModel transferModel;
            var rect = Overlay.SelectionRect;

            if (SnapMode == SnapMode.Element && _workingArranger.Layout == ArrangerLayout.Tiled)
            {
                // Clone a subsection of the arranger and show the full subarranger
                var arranger = _workingArranger.CloneArranger(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
                transferModel = new ArrangerTransferModel(arranger, 0, 0, rect.SnappedWidth, rect.SnappedHeight);
            }
            else
            {
                // Clone the entire arranger and show a subsection of the cloned arranger
                var arranger = _workingArranger.CloneArranger();
                transferModel = new ArrangerTransferModel(arranger, rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
            }

            var editEvent = new EditArrangerPixelsEvent(transferModel);
            _events.PublishOnUIThread(editEvent);
            CancelOverlay();
        }

        public virtual void SelectAll()
        {
            CancelOverlay();
            Overlay.StartSelection(_workingArranger, SnapMode, 0, 0);
            Overlay.UpdateSelectionEndPoint(_workingArranger.ArrangerPixelSize.Width, _workingArranger.ArrangerPixelSize.Height);
            Overlay.CompleteSelection();
        }

        public virtual void CancelOverlay()
        {
            Overlay.Cancel();
            CanPasteElements = false;
            CanPastePixels = false;
            NotifyOfPropertyChange(() => CanEditSelection);
        }

        protected virtual void CreateGridlines()
        {
            if (_workingArranger is null || !CanShowGridlines)
                return;

            _gridlines = new BindableCollection<Gridline>();
            for (int x = 0; x < _workingArranger.ArrangerElementSize.Width; x++) // Vertical gridlines
            {
                var gridline = new Gridline(x * _workingArranger.ElementPixelSize.Width * Zoom + 1, 0,
                    x * _workingArranger.ElementPixelSize.Width * Zoom + 1, _workingArranger.ArrangerPixelSize.Height * Zoom);
                _gridlines.Add(gridline);
            }

            _gridlines.Add(new Gridline(_workingArranger.ArrangerPixelSize.Width * Zoom, 0,
                _workingArranger.ArrangerPixelSize.Width * Zoom, _workingArranger.ArrangerPixelSize.Height * Zoom));

            for (int y = 0; y < _workingArranger.ArrangerElementSize.Height; y++) // Horizontal gridlines
            {
                var gridline = new Gridline(0, y * _workingArranger.ElementPixelSize.Height * Zoom + 1,
                    _workingArranger.ArrangerPixelSize.Width * Zoom, y * _workingArranger.ElementPixelSize.Height * Zoom + 1);
                _gridlines.Add(gridline);
            }

            _gridlines.Add(new Gridline(0, _workingArranger.ArrangerPixelSize.Height * Zoom,
                _workingArranger.ArrangerPixelSize.Width * Zoom, _workingArranger.ArrangerPixelSize.Height * Zoom));

            NotifyOfPropertyChange(() => Gridlines);
        }

        public virtual void ApplyPasteAsPixels()
        {
            var sourceStart = new System.Drawing.Point(Overlay.SelectionRect.SnappedLeft, Overlay.SelectionRect.SnappedTop);
            var destStart = new System.Drawing.Point(Overlay.PasteRect.SnappedLeft, Overlay.PasteRect.SnappedTop);
            int copyWidth = Overlay.SelectionRect.SnappedWidth;
            int copyHeight = Overlay.SelectionRect.SnappedHeight;

            MagitekResult result;

            if (Overlay.CopyArranger.ColorType == PixelColorType.Indexed && _workingArranger.ColorType == PixelColorType.Indexed)
            {
                var sourceImage = new IndexedImage(Overlay.CopyArranger, _paletteService?.DefaultPalette);
                sourceImage.Render();
                result = ImageCopier.CopyPixels(sourceImage, _indexedImage, sourceStart, destStart, copyWidth, copyHeight,
                    ImageRemapOperation.RemapByExactPaletteColors, ImageRemapOperation.RemapByExactIndex);
            }
            else if (Overlay.CopyArranger.ColorType == PixelColorType.Indexed && _workingArranger.ColorType == PixelColorType.Direct)
            {
                var sourceImage = new IndexedImage(Overlay.CopyArranger, _paletteService?.DefaultPalette);
                sourceImage.Render();
                result = ImageCopier.CopyPixels(sourceImage, _directImage, sourceStart, destStart, copyWidth, copyHeight);
            }
            else if (Overlay.CopyArranger.ColorType == PixelColorType.Direct && _workingArranger.ColorType == PixelColorType.Indexed)
            {
                var sourceImage = new DirectImage(Overlay.CopyArranger);
                sourceImage.Render();
                result = ImageCopier.CopyPixels(sourceImage, _indexedImage, sourceStart, destStart, copyWidth, copyHeight,
                    ImageRemapOperation.RemapByExactPaletteColors, ImageRemapOperation.RemapByExactIndex);
            }
            else if (Overlay.CopyArranger.ColorType == PixelColorType.Direct && _workingArranger.ColorType == PixelColorType.Direct)
            {
                var sourceImage = new DirectImage(Overlay.CopyArranger);
                sourceImage.Render();
                result = ImageCopier.CopyPixels(sourceImage, _directImage, sourceStart, destStart, copyWidth, copyHeight);
            }
            else
                throw new InvalidOperationException($"{nameof(ApplyPasteAsPixels)} attempted to copy from an arranger of type {Overlay.CopyArranger.ColorType} to {_workingArranger.ColorType}");

            var notifyEvent = result.Match(
                success =>
                {
                    if (_workingArranger.ColorType == PixelColorType.Indexed)
                        _indexedImage.SaveImage();
                    else if (_workingArranger.ColorType == PixelColorType.Direct)
                        _directImage.SaveImage();

                    Render();
                    return new NotifyOperationEvent("Paste successfully applied");
                },
                fail => new NotifyOperationEvent(fail.Reason)
                );

            _events.PublishOnUIThread(notifyEvent);
        }

        public void ZoomIn() => Zoom = Math.Clamp(Zoom + 1, MinZoom, MaxZoom);
        public void ZoomOut() => Zoom = Math.Clamp(Zoom - 1, MinZoom, MaxZoom);
        
        public void ToggleGridlineVisibility()
        {
            if (CanShowGridlines)
                ShowGridlines ^= true;
        }

        public virtual void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            if (Overlay.State == OverlayState.Selecting)
                Overlay.UpdateSelectionEndPoint(e.X / Zoom, e.Y / Zoom);

            if (Overlay.State == OverlayState.Selecting || Overlay.State == OverlayState.Selected)
            {
                string notifyMessage;
                var rect = Overlay.SelectionRect;
                if (rect.SnapMode == SnapMode.Element)
                    notifyMessage = $"Element Selection: {rect.SnappedWidth / _workingArranger.ElementPixelSize.Width} x {rect.SnappedHeight / _workingArranger.ElementPixelSize.Height}" +
                        $" at ({rect.SnappedLeft / _workingArranger.ElementPixelSize.Width}, {rect.SnappedRight / _workingArranger.ElementPixelSize.Height})";
                else
                    notifyMessage = $"Pixel Selection: {rect.SnappedWidth} x {rect.SnappedHeight}" +
                        $" at ({rect.SnappedLeft} x {rect.SnappedTop})";
                var notifyEvent = new NotifyStatusEvent(notifyMessage, NotifyStatusDuration.Indefinite);
                _events.PublishOnUIThread(notifyEvent);
            }
            else
            {
                var notifyMessage = $"{_workingArranger.Name}: ({(int)Math.Truncate(e.X / Zoom)}, {(int)Math.Truncate(e.Y / Zoom)})";
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
            if (Overlay.State == OverlayState.Selecting)
            {
                Overlay.CompleteSelection();
                if (Overlay.SelectionRect.SnappedWidth == 0 || Overlay.SelectionRect.SnappedHeight == 0)
                    Overlay.Cancel();
            }

            NotifyOfPropertyChange(() => CanEditSelection);
        }

        public virtual void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            if (Overlay.State == OverlayState.Selected && e.LeftButton && Overlay.SelectionRect.ContainsPointSnapped(e.X / Zoom, e.Y / Zoom))
            {
                // Start drag for selection (Handled by DragDrop in View)
            }
            else if ((Overlay.State == OverlayState.Pasting || Overlay.State == OverlayState.Pasted) && 
                e.LeftButton && Overlay.PasteRect.ContainsPointSnapped(e.X / Zoom, e.Y / Zoom))
            {
                // Start drag for paste (Handled by DragDrop in View)
            }
            //else if ((Overlay.State == OverlayState.Selected || Overlay.State == OverlayState.Pasted || Overlay.State == OverlayState.Pasting) && 
            //    e.RightButton)
            //{
            //    CancelOverlay();
            //    NotifyOfPropertyChange(() => CanEditSelection);
            //}
            else if (e.LeftButton)
            {
                Overlay.StartSelection(_workingArranger, SnapMode, e.X / Zoom, e.Y / Zoom);
            }
        }

        public virtual void OnMouseWheel(object sender, MouseCaptureArgs e) 
        {
            if (e.WheelDelta > 0)
                ZoomIn();
            else
                ZoomOut();
        }

        protected virtual bool CanAcceptTransfer(ArrangerTransferModel model) => true;

        public virtual void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ArrangerTransferModel model)
            {
                model.DestinationArranger = _workingArranger;
                Overlay.UpdatePastingStartPoint(dropInfo.DropPosition.X, dropInfo.DropPosition.Y, SnapMode);
                Overlay.CompletePasting();
            }
        }

        public virtual void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ArrangerTransferModel model)
            {
                if (CanAcceptTransfer(model))
                {
                    if (Overlay.State != OverlayState.Pasting)
                    {
                        Overlay.StartSelection(model.Arranger, SnapMode.Pixel, model.X, model.Y);
                        Overlay.UpdateSelectionEndPoint(model.X + model.Width, model.Y + model.Height);
                        Overlay.CompleteSelection();
                        Overlay.StartPasting(_workingArranger, SnapMode, dropInfo.DropPosition.X, dropInfo.DropPosition.Y);
                    }
                    else if (Overlay.State == OverlayState.Pasting)
                        Overlay.UpdatePastingStartPoint(dropInfo.DropPosition.X, dropInfo.DropPosition.Y, SnapMode);

                    dropInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;
                }
            }
        }

        public virtual void StartDrag(IDragInfo dragInfo)
        {
            var rect = Overlay.SelectionRect;
            var transferModel = new ArrangerTransferModel(_workingArranger, rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
            dragInfo.Data = transferModel;
            dragInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;

            CancelOverlay();
        }

        public virtual bool CanStartDrag(IDragInfo dragInfo)
        {
            if (Overlay.State == OverlayState.Selected)
                return Overlay.SelectionRect.ContainsPointSnapped(dragInfo.DragStartPosition.X, dragInfo.DragStartPosition.Y);
            else if (Overlay.State == OverlayState.Pasting || Overlay.State == OverlayState.Pasted)
                return Overlay.PasteRect.ContainsPointSnapped(dragInfo.DragStartPosition.X, dragInfo.DragStartPosition.Y);
            else
                return false;
        }

        public virtual void Dropped(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ArrangerTransferModel model)
            {
                if (!ReferenceEquals(model.DestinationArranger, _workingArranger))
                {
                    CancelOverlay();
                }
            }
        }

        public virtual void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo) { }

        public virtual void DragCancelled()
        {
            CancelOverlay();
            CanPasteElements = false;
            CanPastePixels = false;
        }
        public virtual bool TryCatchOccurredException(Exception exception) => false;
    }
}
