using ImageMagitek;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TileShop.Shared.Models
{
    public enum OverlayState { None, Selecting, Selected, Pasting, Pasted }
    public class ArrangerOverlay : INotifyPropertyChanged
    {
        private Arranger _copyArranger;
        private Arranger _pasteArranger;

        private OverlayState _overlayState;
        public OverlayState State
        {
            get => _overlayState;
            set => SetField(ref _overlayState, value);
        }

        private SnappedRectangle _selectionRect;
        public SnappedRectangle SelectionRect
        {
            get => _selectionRect;
            set => SetField(ref _selectionRect, value);
        }

        private SnappedRectangle _pasteRect;
        public SnappedRectangle PasteRect
        {
            get => _pasteRect;
            set => SetField(ref _pasteRect, value);
        }

        /// <summary>
        /// Starts a new selection
        /// </summary>
        /// <param name="copyArranger">Source arranger for the copy</param>
        /// <param name="snapMode">SnapMode for selection</param>
        /// <param name="x">X-coordinate of selection end point in pixel coordinates</param>
        /// <param name="y">Y-coordinate of selection end point in pixel coordinates</param>
        public void StartSelection(Arranger copyArranger, SnapMode snapMode, double x, double y)
        {
            _copyArranger = copyArranger;
            State = OverlayState.Selecting;

            SelectionRect = new SnappedRectangle(_copyArranger.ArrangerPixelSize, _copyArranger.ElementPixelSize, snapMode);
            SelectionRect.SetBounds(x, x, y, y);
        }

        /// <summary>
        /// Updates the endpoint for the selection
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void UpdateSelectionEndPoint(double x, double y)
        {
            if (State == OverlayState.Selecting)
            {
                SelectionRect.Right = x;
                SelectionRect.Bottom = y;
            }
        }

        /// <summary>
        /// Completes the active selection and stops it from resizing
        /// </summary>
        public void CompleteSelection()
        {
            if (State == OverlayState.Selecting)
            {
                State = OverlayState.Selected;
                OnPropertyChanged(nameof(SelectionRect));
            }
        }

        /// <summary>
        /// Starts a new paste if there is a finalized selection
        /// </summary>
        /// <param name="copyArranger">Source arranger for the copy</param>
        /// <param name="snapMode">SnapMode for selection</param>
        /// <param name="x">X-coordinate of selection end point in pixel coordinates</param>
        /// <param name="y">Y-coordinate of selection end point in pixel coordinates</param>
        public void StartPasting(Arranger pasteArranger, SnapMode snapMode, double x, double y)
        {
            if (State == OverlayState.Selected || State == OverlayState.Pasting)
            {
                _pasteArranger = pasteArranger;
                State = OverlayState.Pasting;

                PasteRect = new SnappedRectangle(_pasteArranger.ArrangerPixelSize, _pasteArranger.ArrangerElementSize, snapMode);
                PasteRect.Left = x;
                PasteRect.Right = x;
                PasteRect.Right = x + SelectionRect.SnappedWidth;
                PasteRect.Bottom = y + SelectionRect.SnappedHeight;
            }
        }

        public void UpdatePastingStartPoint(double x, double y)
        {
            if (State == OverlayState.Pasting)
            {
                PasteRect.Left = x;
                PasteRect.Top = y;
                PasteRect.Right = x + SelectionRect.SnappedWidth;
                PasteRect.Bottom = y + SelectionRect.SnappedHeight;
            }
        }

        public void CompletePasting()
        {
            if (State == OverlayState.Pasting)
                State = OverlayState.Pasted;
        }

        /// <summary>
        /// Cancels any selection or paste state
        /// </summary>
        public void Cancel()
        {
            State = OverlayState.None;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
