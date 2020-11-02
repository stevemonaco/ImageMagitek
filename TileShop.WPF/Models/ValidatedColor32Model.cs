using System.Windows.Media;
using ImageMagitek.Colors;
using Stylet;

namespace TileShop.WPF.Models
{
    public class ValidatedColor32Model : PropertyChangedBase
    {
        private IColor32 _foreignColor;
        private readonly IColorFactory _colorFactory;

        public IColor32 WorkingColor { get; set; }

        private Color _color;
        public Color Color
        {
            get => _color;
            set => SetAndNotify(ref _color, value);
        }

        public int Red
        {
            get => WorkingColor.R;
            set
            {
                WorkingColor.R = (byte) value;
                OnPropertyChanged(nameof(Red));
                var nativeColor = _colorFactory.ToNative(WorkingColor);
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
                var nativeColor = _colorFactory.ToNative(WorkingColor);
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
                var nativeColor = _colorFactory.ToNative(WorkingColor);
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
                var nativeColor = _colorFactory.ToNative(WorkingColor);
                Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
                OnPropertyChanged(nameof(CanSaveColor));
            }
        }

        private int _redMax;
        public int RedMax
        {
            get => _redMax;
            set => SetAndNotify(ref _redMax, value);
        }

        private int _greenMax;
        public int GreenMax
        {
            get => _greenMax;
            set => SetAndNotify(ref _greenMax, value);
        }

        private int _blueMax;
        public int BlueMax
        {
            get => _blueMax;
            set => SetAndNotify(ref _blueMax, value);
        }

        private int _alphaMax;
        public int AlphaMax
        {
            get => _alphaMax;
            set => SetAndNotify(ref _alphaMax, value);
        }

        public bool CanSaveColor
        {
            get => WorkingColor.Color != _foreignColor.Color;
        }

        private int _index;
        public int Index
        {
            get => _index;
            set => SetAndNotify(ref _index, value);
        }

        public ValidatedColor32Model(IColor32 foreignColor, int index, IColorFactory colorFactory)
        {
            _foreignColor = foreignColor;
            Index = index;
            _colorFactory = colorFactory;

            WorkingColor = (IColor32)_colorFactory.CloneColor(foreignColor);
            var nativeColor = _colorFactory.ToNative(foreignColor);
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

        public void SaveColor()
        {
            _foreignColor = (IColor32)_colorFactory.CloneColor(WorkingColor);
            OnPropertyChanged(nameof(CanSaveColor));
        }
    }
}
