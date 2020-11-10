using System;

namespace ImageMagitek.Colors.Converters
{
    public class ColorConverterNes : IColorConverter<ColorNes>
    {
        private readonly Palette _nesPalette;

        public ColorConverterNes(Palette nesPalette)
        {
            _nesPalette = nesPalette;
        }

        public ColorNes ToForeignColor(ColorRgba32 nc)
        {
            if (_nesPalette.TryGetIndexByNativeColor(nc, ColorMatchStrategy.Nearest, out var index))
            {
                return (ColorNes) _nesPalette.GetForeignColor(index);
            }
            throw new ArgumentException($"{nameof(ToForeignColor)} parameter (R: {nc.R}, G: {nc.G}, B: {nc.B}, A: {nc.A}) could not be matched in palette '{_nesPalette.Name}'");
        }

        public ColorRgba32 ToNativeColor(ColorNes fc)
        {
            return _nesPalette.GetNativeColor((int) fc.Color);
        }
    }
}
