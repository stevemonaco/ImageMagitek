using Caliburn.Micro;
using ImageMagitek;
using System;
using TileShop.WPF.Behaviors;
using TileShop.Shared.EventModels;
using TileShop.WPF.Helpers;
using TileShop.WPF.Models;
using System.Drawing;
using GongSolutions.Wpf.DragDrop;
using TileShop.Shared.Models;

namespace TileShop.WPF.ViewModels
{
    public enum EditMode { ArrangeGraphics, ModifyGraphics }

    public class ScatteredArrangerEditorViewModel : ArrangerEditorViewModel, IDropTarget
    {
        public bool CanChangeSnapMode => _arranger.Layout == ArrangerLayout.TiledArranger;

        private EditMode _editMode = EditMode.ArrangeGraphics;
        public EditMode EditMode
        {
            get => _editMode;
            set => Set(ref _editMode, value);
        }

        private ArrangerSelector _selection;
        public ArrangerSelector Selection
        {
            get => _selection;
            set => Set(ref _selection, value);
        }

        public ScatteredArrangerEditorViewModel(Arranger arranger, IEventAggregator events)
        {
            Resource = arranger;
            _arranger = arranger;
            _events = events;

            _arrangerImage = new ArrangerImage(_arranger);
            _arrangerImage.Render();
            ArrangerSource = new ImageRgba32Source(_arrangerImage.Image);
            CreateGridlines();

            if (arranger.Layout == ArrangerLayout.TiledArranger)
                Selection = new ArrangerSelector(_arranger.ArrangerPixelSize, _arranger.ElementPixelSize, SnapMode.Element);
            else
                Selection = new ArrangerSelector(_arranger.ArrangerPixelSize, _arranger.ElementPixelSize, SnapMode.Pixel);
        }

        public bool CanEditSelection => true;

        public void EditSelection()
        {
            ArrangerTransferModel transferModel;

            if (Selection.SnapMode == SnapMode.Element)
                transferModel = new ArrangerTransferModel(_arranger, Selection.SnappedX1, Selection.SnappedY1, Selection.SnappedWidth, Selection.SnappedHeight);
            else
                transferModel = new ArrangerTransferModel(_arranger, Selection.SnappedX1, Selection.SnappedY1, Selection.SnappedWidth, Selection.SnappedHeight);

            var editEvent = new EditArrangerPixelsEvent(transferModel);
            _events.PublishOnUIThreadAsync(editEvent);
        }

        public void CancelSelection() => Selection.CancelSelection();

        public override void OnMouseMove(object sender, MouseCaptureArgs e)
        {
            if(Selection.IsSelecting)
                Selection.UpdateSelection(e.X / Zoom, e.Y / Zoom);

            if(Selection.HasSelection)
            {
                string notifyMessage;
                if(Selection.SnapMode == SnapMode.Element)
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

        public override void OnMouseLeave(object sender, MouseCaptureArgs e)
        {
            var notifyEvent = new NotifyStatusEvent("", NotifyStatusDuration.Indefinite);
            _events.PublishOnUIThreadAsync(notifyEvent);
        }

        public override void OnMouseUp(object sender, MouseCaptureArgs e)
        {
            Selection.StopSelection();
        }

        public override void OnMouseDown(object sender, MouseCaptureArgs e)
        {
            if(!Selection.HasSelection && e.LeftButton)
            {
                Selection.StartSelection(e.X / Zoom, e.Y / Zoom);
            }
            if(Selection.HasSelection && e.LeftButton)
            {
                if (Selection.IsPointInSelection(e.X / Zoom, e.Y / Zoom)) // Start drag
                    return;
                else // New selection
                    Selection.StartSelection(e.X / Zoom, e.Y / Zoom);
            }
            if(Selection.HasSelection && e.RightButton)
            {
                Selection.CancelSelection();
            }
        }

        public void DragOver(IDropInfo dropInfo)
        {
            throw new NotImplementedException();
        }

        public void Drop(IDropInfo dropInfo)
        {
            throw new NotImplementedException();
        }
    }
}
