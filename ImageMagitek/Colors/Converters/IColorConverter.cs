namespace ImageMagitek.Colors.Converters;

public interface IColorConverter<TColor> where TColor : IColor
{
    TColor ToForeignColor(ColorRgba32 nc);
    ColorRgba32 ToNativeColor(TColor fc);
}
