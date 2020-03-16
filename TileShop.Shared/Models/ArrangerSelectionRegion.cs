using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;

namespace TileShop.Shared.Models
{
    public class ArrangerSelectionRegion : INotifyPropertyChanged
    {
        private double _x1;
        private double _x2;
        private double _y1;
        private double _y2;

        private Size _arrangerSize;
        private Size _elementSize;

        private int _snappedX1;
        /// <summary>
        /// Left edge of selection in pixel coordinates
        /// </summary>
        public int SnappedX1
        {
            get => _snappedX1;
            set => SetField(ref _snappedX1, value);
        }

        private int _snappedX2;
        /// <summary>
        /// Right edge of selection in pixel coordinates
        /// </summary>
        public int SnappedX2
        {
            get => _snappedX2;
            set => SetField(ref _snappedX2, value);
        }

        private int _snappedY1;
        /// <summary>
        /// Top edge of selection in pixel coordinates
        /// </summary>
        public int SnappedY1
        {
            get => _snappedY1;
            set => SetField(ref _snappedY1, value);
        }

        private int _snappedY2;
        /// <summary>
        /// Bottom edge of selection in pixel coordinates
        /// </summary>
        public int SnappedY2
        {
            get => _snappedY2;
            set => SetField(ref _snappedY2, value);
        }

        /// <summary>
        /// Width of selection in pixel coordinates
        /// </summary>
        public int SnappedWidth
        {
            get => SnappedX2 - SnappedX1;
        }

        /// <summary>
        /// Height of selection in pixel coordinates
        /// </summary>
        public int SnappedHeight
        {
            get => SnappedY2 - SnappedY1;
        }

        private bool _hasSelection;
        public bool HasSelection
        {
            get => _hasSelection;
            set => SetField(ref _hasSelection, value);
        }

        private bool _isSelecting;
        public bool IsSelecting
        {
            get => _isSelecting;
            set => SetField(ref _isSelecting, value);
        }

        private SnapMode _snapMode;
        public SnapMode SnapMode
        {
            get => _snapMode;
            set
            {
                SetField(ref _snapMode, value);
                UpdateSnappedSelection();
            }
        }

        public ArrangerSelectionRegion(Size arrangerSize, Size elementSize, SnapMode snapMode)
        {
            _arrangerSize = arrangerSize;
            _elementSize = elementSize;
            SnapMode = snapMode;
        }

        /// <summary>
        /// Starts a new selection
        /// </summary>
        /// <param name="x">X-coordinate of selection end point in pixel coordinates</param>
        /// <param name="y">Y-coordinate of selection end point in pixel coordinates</param>
        public void StartSelection(double x, double y)
        {
            _x1 = x;
            _y1 = y;
            _x2 = x;
            _y2 = y;
            HasSelection = true;
            IsSelecting = true;
            UpdateSnappedSelection();
        }

        /// <summary>
        /// Stops the active selection from resizing
        /// </summary>
        public void StopSelection()
        {
            IsSelecting = false;
        }

        /// <summary>
        /// Cancels the active selection
        /// </summary>
        public void CancelSelection()
        {
            IsSelecting = false;
            HasSelection = false;
        }

        public bool IsPointInSelection(double x, double y)
        {
            if (HasSelection)
                return (x >= SnappedX1) && (x <= SnappedX2) && (y >= SnappedY1) && (y <= SnappedY2);
            return false;
        }

        /// <summary>
        /// Update the end point for the selection
        /// </summary>
        /// <param name="x">X-coordinate of selection end point in pixel coordinates</param>
        /// <param name="y">Y-coordinate of selection end point in pixel coordinates</param>
        public void UpdateSelection(double x, double y)
        {
            if(IsSelecting)
            {
                _x2 = x;
                _y2 = y;
                UpdateSnappedSelection();
            }
        }

        private void UpdateSnappedSelection()
        {
            if (SnapMode == SnapMode.Element)
                UpdateSnappedElementSelection();
            else if (SnapMode == SnapMode.Pixel)
                UpdateSnappedPixelSelection();

            OnPropertyChanged(nameof(SnappedWidth));
            OnPropertyChanged(nameof(SnappedHeight));
        }

        private void UpdateSnappedElementSelection()
        {
            SnappedX1 = (int)(Math.Floor(Math.Min(_x1, _x2) / _elementSize.Width) * _elementSize.Width);
            SnappedX2 = (int)(Math.Ceiling(Math.Max(_x1, _x2) / _elementSize.Width) * _elementSize.Width);
            SnappedY1 = (int)(Math.Floor(Math.Min(_y1, _y2) / _elementSize.Width) * _elementSize.Width);
            SnappedY2 = (int)(Math.Ceiling(Math.Max(_y1, _y2) / _elementSize.Width) * _elementSize.Width);
        }

        private void UpdateSnappedPixelSelection()
        {
            SnappedX1 = (int)Math.Round(Math.Min(_x1, _x2));
            SnappedX2 = (int)Math.Round(Math.Max(_x1, _x2));
            SnappedY1 = (int)Math.Round(Math.Min(_y1, _y2));
            SnappedY2 = (int)Math.Round(Math.Max(_y1, _y2));
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
