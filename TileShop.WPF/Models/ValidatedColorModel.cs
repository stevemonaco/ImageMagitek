using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ImageMagitek.Colors;

namespace TileShop.WPF.Models
{
    public class ValidatedColorModel : INotifyPropertyChanged
    {
        private IColor32 _foreignColor;

        public IColor32 WorkingColor { get; set; }

        private Color _color;
        public Color Color
        {
            get => _color;
            set => SetField(ref _color, value);
        }

        public int Red
        {
            get => WorkingColor.R;
            set
            {
                WorkingColor.R = (byte) value;
                OnPropertyChanged(nameof(Red));
                var nativeColor = ImageMagitek.Colors.ColorConverter.ToNative(WorkingColor);
                Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
                OnPropertyChanged(nameof(CanSaveColor));
            }
        }

        public int Blue
        {
            get => WorkingColor.B;
            set
            {
                WorkingColor.B = (byte)value;
                OnPropertyChanged(nameof(Blue));
                var nativeColor = ImageMagitek.Colors.ColorConverter.ToNative(WorkingColor);
                Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
                OnPropertyChanged(nameof(CanSaveColor));
            }
        }

        public int Green
        {
            get => WorkingColor.G;
            set
            {
                WorkingColor.G = (byte)value;
                OnPropertyChanged(nameof(Green));
                var nativeColor = ImageMagitek.Colors.ColorConverter.ToNative(WorkingColor);
                Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
                OnPropertyChanged(nameof(CanSaveColor));
            }
        }

        public int Alpha
        {
            get => WorkingColor.A;
            set
            {
                WorkingColor.A = (byte)value;
                OnPropertyChanged(nameof(Alpha));
                var nativeColor = ImageMagitek.Colors.ColorConverter.ToNative(WorkingColor);
                Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
                OnPropertyChanged(nameof(CanSaveColor));
            }
        }

        private int _redMax;
        public int RedMax
        {
            get => _redMax;
            set => SetField(ref _redMax, value);
        }

        private int _greenMax;
        public int GreenMax
        {
            get => _greenMax;
            set => SetField(ref _greenMax, value);
        }

        private int _blueMax;
        public int BlueMax
        {
            get => _blueMax;
            set => SetField(ref _blueMax, value);
        }

        private int _alphaMax;
        public int AlphaMax
        {
            get => _alphaMax;
            set => SetField(ref _alphaMax, value);
        }

        public bool CanSaveColor
        {
            get
            {
                if (WorkingColor.Color == _foreignColor.Color)
                    return false;
                return true;
            }
        }

        public int Index { get; set; }

        public ValidatedColorModel(IColor32 foreignColor, int index)
        {
            _foreignColor = foreignColor;
            Index = index;
            WorkingColor = ColorFactory.CloneColor(foreignColor);
            var nativeColor = ImageMagitek.Colors.ColorConverter.ToNative(foreignColor);
            Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);

            Red = foreignColor.R;
            Green = foreignColor.G;
            Blue = foreignColor.B;
            Alpha = foreignColor.A;
            RedMax = foreignColor.RedMax;
            GreenMax = foreignColor.GreenMax;
            BlueMax = foreignColor.BlueMax;
            AlphaMax = foreignColor.AlphaMax;
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
