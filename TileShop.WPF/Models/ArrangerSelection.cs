using ImageMagitek;
using Stylet;
using TileShop.Shared.Models;

namespace TileShop.WPF.Models
{
    public class ArrangerSelection : PropertyChangedBase
    {
        public Arranger Arranger { get; private set; }

        private SnappedRectangle _selectionRect;
        public SnappedRectangle SelectionRect
        {
            get => _selectionRect;
            private set => SetAndNotify(ref _selectionRect, value);
        }

        private SnapMode _snapMode;
        public SnapMode SnapMode
        {
            get => _snapMode;
            set
            {
                SelectionRect.SnapMode = value;
                SetAndNotify(ref _snapMode, value);
            }
        }

        private bool _hasSelection;
        public bool HasSelection
        {
            get => _hasSelection;
            set => SetAndNotify(ref _hasSelection, value);
        }

        public ArrangerSelection(Arranger arranger, SnapMode snapMode)
        {
            Arranger = arranger;
            SelectionRect = new SnappedRectangle(Arranger.ArrangerPixelSize, Arranger.ElementPixelSize, snapMode, ElementSnapRounding.Expand);
            SnapMode = snapMode;
        }

        /// <summary>
        /// Starts a new selection
        /// </summary>
        /// <param name="x">X-coordinate of selection end point in pixel coordinates</param>
        /// <param name="y">Y-coordinate of selection end point in pixel coordinates</param>
        public void StartSelection(double x, double y)
        {
            SelectionRect.SetBounds(x, x, y, y);
            HasSelection = true;
        }

        /// <summary>
        /// Updates the endpoint for the selection
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void UpdateSelectionEndpoint(double x, double y)
        {
            if (HasSelection)
                SelectionRect.SetEndpoint(x, y);
        }

        /// <summary>
        /// Cancels any selection
        /// </summary>
        public void Cancel()
        {
            HasSelection = false;
        }
    }
}
