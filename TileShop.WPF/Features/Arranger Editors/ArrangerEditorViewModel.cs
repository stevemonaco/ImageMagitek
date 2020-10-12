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

        private BitmapAdapter _bitmapAdapter;
        public BitmapAdapter BitmapAdapter
        {
            get => _bitmapAdapter;
            set => SetAndNotify(ref _bitmapAdapter, value);
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

        public bool CanChangeSnapMode { get; protected set; }

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
                if (Selection.HasSelection)
                {
                    var rect = Selection.SelectionRect;
                    if (rect.SnappedWidth == 0 || rect.SnappedHeight == 0)
                        return false;

                    return !_workingArranger.EnumerateElementsByPixel(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight)
                        .Any(x => x is null || x?.DataFile is null || x?.Codec is BlankIndexedCodec || x?.Codec is BlankDirectCodec);
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
                if (Selection is object)
                    Selection.SnapMode = SnapMode;
            }
        }

        private ArrangerSelection _selection;
        public ArrangerSelection Selection
        {
            get => _selection;
            set => SetAndNotify(ref _selection, value);
        }

        private bool _isSelecting;
        public bool IsSelecting
        {
            get => _isSelecting;
            set => SetAndNotify(ref _isSelecting, value);
        }

        public bool CanAcceptPixelPastes { get; set; }
        public bool CanAcceptElementPastes { get; set; }

        private ArrangerPaste _paste;
        public ArrangerPaste Paste
        {
            get => _paste;
            set => SetAndNotify(ref _paste, value);
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
            var rect = Selection.SelectionRect;

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
            Selection = new ArrangerSelection(_workingArranger, SnapMode);
            Selection.StartSelection(0, 0);
            Selection.UpdateSelectionEndpoint(_workingArranger.ArrangerPixelSize.Width, _workingArranger.ArrangerPixelSize.Height);
        }

        public virtual void CancelOverlay()
        {
            Selection = new ArrangerSelection(_workingArranger, SnapMode);
            Paste = null;

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

        public abstract void ApplyPaste(ArrangerPaste paste);

        //private void ApplyPasteAsPixels()
        //{
        //    var sourceStart = new System.Drawing.Point(Paste.Rect.SnappedLeft, Paste.Rect.SnappedTop);
        //    var destStart = new System.Drawing.Point(Paste.Rect.SnappedLeft, Paste.Rect.SnappedTop);
        //    int copyWidth = Paste.Rect.SnappedWidth;
        //    int copyHeight = Paste.Rect.SnappedHeight;

        //    MagitekResult result;

        //    if (Paste.Copy.Source.ColorType == PixelColorType.Indexed && _workingArranger.ColorType == PixelColorType.Indexed)
        //    {
        //        var sourceImage = (Paste.OverlayImage as IndexedBitmapAdapter).Image;
        //        result = ImageCopier.CopyPixels(sourceImage, _indexedImage, sourceStart, destStart, copyWidth, copyHeight,
        //            ImageRemapOperation.RemapByExactPaletteColors, ImageRemapOperation.RemapByExactIndex);
        //    }
        //    else if (Paste.Copy.Source.ColorType == PixelColorType.Indexed && _workingArranger.ColorType == PixelColorType.Direct)
        //    {
        //        var sourceImage = (Paste.OverlayImage as IndexedBitmapAdapter).Image;
        //        result = ImageCopier.CopyPixels(sourceImage, _directImage, sourceStart, destStart, copyWidth, copyHeight);
        //    }
        //    else if (Paste.Copy.Source.ColorType == PixelColorType.Direct && _workingArranger.ColorType == PixelColorType.Indexed)
        //    {
        //        var sourceImage = (Paste.OverlayImage as DirectBitmapAdapter).Image;
        //        result = ImageCopier.CopyPixels(sourceImage, _indexedImage, sourceStart, destStart, copyWidth, copyHeight,
        //            ImageRemapOperation.RemapByExactPaletteColors, ImageRemapOperation.RemapByExactIndex);
        //    }
        //    else if (Paste.Copy.Source.ColorType == PixelColorType.Direct && _workingArranger.ColorType == PixelColorType.Direct)
        //    {
        //        var sourceImage = (Paste.OverlayImage as DirectBitmapAdapter).Image;
        //        result = ImageCopier.CopyPixels(sourceImage, _directImage, sourceStart, destStart, copyWidth, copyHeight);
        //    }
        //    else
        //        throw new InvalidOperationException($"{nameof(ApplyPasteAsPixels)} attempted to copy from an arranger of type {Paste.Copy.Source.ColorType} to {_workingArranger.ColorType}");

        //    var notifyEvent = result.Match(
        //        success =>
        //        {
        //            if (_workingArranger.ColorType == PixelColorType.Indexed)
        //                _indexedImage.SaveImage();
        //            else if (_workingArranger.ColorType == PixelColorType.Direct)
        //                _directImage.SaveImage();

        //            Render();
        //            return new NotifyOperationEvent("Paste successfully applied");
        //        },
        //        fail => new NotifyOperationEvent(fail.Reason)
        //        );

        //    _events.PublishOnUIThread(notifyEvent);
        //}

        public void ZoomIn() => Zoom = Math.Clamp(Zoom + 1, MinZoom, MaxZoom);
        public void ZoomOut() => Zoom = Math.Clamp(Zoom - 1, MinZoom, MaxZoom);
        
        public void ToggleGridlineVisibility()
        {
            if (CanShowGridlines)
                ShowGridlines ^= true;
        }

        #region Mouse Actions
        public virtual void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            if (IsSelecting)
                Selection.UpdateSelectionEndpoint(e.X / Zoom, e.Y / Zoom);

            if (Selection.HasSelection)
            {
                string notifyMessage;
                var rect = Selection.SelectionRect;
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
            if (IsSelecting)
            {
                IsSelecting = false;

                if (Selection.SelectionRect.SnappedWidth == 0 || Selection.SelectionRect.SnappedHeight == 0)
                {
                    Selection = new ArrangerSelection(_workingArranger, SnapMode);
                }

                NotifyOfPropertyChange(() => CanEditSelection);
            }
        }

        public virtual void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            int x = (int)(e.X / Zoom);
            int y = (int)(e.Y / Zoom);

            if (e.LeftButton && Paste is object && !Paste.Rect.ContainsPointSnapped(x, y))
            {
                ApplyPaste(Paste);
                Paste = null;
            }

            if (Selection?.HasSelection is true && e.LeftButton && Selection.SelectionRect.ContainsPointSnapped(x, y))
            {
                // Start drag for selection (Handled by DragDrop in View)
            }
            else if (Paste is object && e.LeftButton && Paste.Rect.ContainsPointSnapped(x, y))
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
                Selection.StartSelection(x, y);
                IsSelecting = true;
            }
        }

        public virtual void OnMouseWheel(object sender, MouseCaptureArgs e)
        {
            if (e.WheelDelta > 0)
                ZoomIn();
            else
                ZoomOut();
        }
        #endregion

        #region Drag and Drop Implementation
        public virtual void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ArrangerPaste paste)
            {
                paste.SnapMode = SnapMode;
                Paste = paste;
                Paste.MoveTo((int)dropInfo.DropPosition.X, (int)dropInfo.DropPosition.Y);
            }
        }

        public virtual void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ArrangerPaste paste)
            {
                if (paste.Copy is ElementCopy && !CanAcceptElementPastes)
                    return;

                if ((paste.Copy is IndexedPixelCopy || paste.Copy is DirectPixelCopy) && !CanAcceptPixelPastes)
                    return;

                if (!ReferenceEquals(dropInfo.DragInfo.SourceItem, this))
                    (dropInfo.DragInfo.SourceItem as ArrangerEditorViewModel).CancelOverlay();

                if (Paste != paste)
                {
                    Paste = new ArrangerPaste(paste.Copy, SnapMode);
                    Paste.DeltaX = paste.DeltaX;
                    Paste.DeltaY = paste.DeltaY;
                }

                Paste.MoveTo((int)dropInfo.DropPosition.X, (int)dropInfo.DropPosition.Y);
                dropInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;
            }
        }

        public virtual void StartDrag(IDragInfo dragInfo)
        {
            if (Selection.HasSelection)
            {
                var rect = Selection.SelectionRect;

                ArrangerCopy copy = default;
                if (SnapMode == SnapMode.Element)
                {
                    int x = rect.SnappedLeft / _workingArranger.ElementPixelSize.Width;
                    int y = rect.SnappedTop / _workingArranger.ElementPixelSize.Height;
                    int width = rect.SnappedWidth / _workingArranger.ElementPixelSize.Width;
                    int height = rect.SnappedHeight / _workingArranger.ElementPixelSize.Height;
                    copy = _workingArranger.CopyElements(x, y, width, height);
                }
                else if (SnapMode == SnapMode.Pixel && _workingArranger.ColorType == PixelColorType.Indexed)
                {
                    copy = _workingArranger.CopyPixelsIndexed(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
                }
                else if (SnapMode == SnapMode.Pixel && _workingArranger.ColorType == PixelColorType.Direct)
                {
                    copy = _workingArranger.CopyPixelsDirect(rect.SnappedLeft, rect.SnappedTop, rect.SnappedWidth, rect.SnappedHeight);
                }

                var paste = new ArrangerPaste(copy, SnapMode);
                paste.DeltaX = (int) dragInfo.DragStartPosition.X - Selection.SelectionRect.SnappedLeft;
                paste.DeltaY = (int) dragInfo.DragStartPosition.Y - Selection.SelectionRect.SnappedTop;
                dragInfo.Data = paste;
                dragInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;

                Selection = new ArrangerSelection(_workingArranger, SnapMode);
            }
            else if (Paste is object)
            {
                Paste.DeltaX = (int)dragInfo.DragStartPosition.X - Paste.Rect.SnappedLeft;
                Paste.DeltaY = (int)dragInfo.DragStartPosition.Y - Paste.Rect.SnappedTop;
                Paste.SnapMode = SnapMode;

                dragInfo.Data = Paste;
                dragInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;
            }
        }

        public virtual bool CanStartDrag(IDragInfo dragInfo)
        {
            if (Selection.HasSelection)
            {
                return Selection.SelectionRect.ContainsPointSnapped(dragInfo.DragStartPosition.X, dragInfo.DragStartPosition.Y);
            }
            else if (Paste is object)
            {
                return Paste.Rect.ContainsPointSnapped(dragInfo.DragStartPosition.X, dragInfo.DragStartPosition.Y);
            }
            else
                return false;
        }

        public virtual void Dropped(IDropInfo dropInfo) { }

        public virtual void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo) { }

        public virtual void DragCancelled()
        {
            CancelOverlay();
        }
        public virtual bool TryCatchOccurredException(Exception exception) => false;
        #endregion
    }
}
