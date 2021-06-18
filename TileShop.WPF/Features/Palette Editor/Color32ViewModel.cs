using ImageMagitek.Colors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace TileShop.WPF.ViewModels
{
    public class Color32ViewModel : EditableColorBaseViewModel
    {
        private IColor32 _foreignColor;
        private readonly IColorFactory _colorFactory;

        public override bool CanSaveColor
        {
            get => WorkingColor.Color != _foreignColor.Color;
        }

        public int Red
        {
            get => ((IColor32)WorkingColor).R;
            set
            {
                ((IColor32)WorkingColor).R = (byte)value;
                OnPropertyChanged(nameof(Red));
                var nativeColor = _colorFactory.ToNative(WorkingColor);
                Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
                OnPropertyChanged(nameof(CanSaveColor));
            }
        }

        public int Blue
        {
            get => ((IColor32)WorkingColor).B;
            set
            {
                ((IColor32)WorkingColor).B = (byte)value;
                OnPropertyChanged(nameof(Blue));
                var nativeColor = _colorFactory.ToNative(WorkingColor);
                Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
                OnPropertyChanged(nameof(CanSaveColor));
            }
        }

        public int Green
        {
            get => ((IColor32)WorkingColor).G;
            set
            {
                ((IColor32)WorkingColor).G = (byte)value;
                OnPropertyChanged(nameof(Green));
                var nativeColor = _colorFactory.ToNative(WorkingColor);
                Color = Color.FromArgb(nativeColor.A, nativeColor.R, nativeColor.G, nativeColor.B);
                OnPropertyChanged(nameof(CanSaveColor));
            }
        }

        public int Alpha
        {
            get => ((IColor32)WorkingColor).A;
            set
            {
                ((IColor32)WorkingColor).A = (byte)value;
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

        public Color32ViewModel(IColor32 foreignColor, int index, IColorFactory colorFactory)
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
