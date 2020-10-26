using System;

namespace ImageMagitek.Colors.ColorFormats
{
    public struct ColorNes : ITableColor
    {
        private uint _color;
        public uint Color
        { 
            get => _color;
            set => _color = Math.Clamp(value, 0, (uint)ColorMax);
        }

        public int Size => 8;
        public int ColorMax => 63;
    }
}
