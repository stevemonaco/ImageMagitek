using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ImageMagitek;
using TileShop.Shared.Models;

namespace TileShop.WPF.Models
{
    public class ArrangerSelection : INotifyPropertyChanged
    {
        public Arranger Arranger { get; private set; }

        private SnappedRectangle _selectionRect;
        public SnappedRectangle SelectionRect
        {
            get => _selectionRect;
            private set => SetField(ref _selectionRect, value);
        }

        private SnapMode _snapMode;
        public SnapMode SnapMode
        {
            get => _snapMode;
            set
            {
                SelectionRect.SnapMode = value;
                SetField(ref _snapMode, value);
            }
        }

        private bool _hasSelection;
        public bool HasSelection
        {
            get => _hasSelection;
            set => SetField(ref _hasSelection, value);
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
