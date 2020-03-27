namespace ImageMagitek.Colors
{
    public interface IColorConverter<TColor> where TColor : IColor32
    {
        TColor ToForeignColor(ColorRgba32 nc);
        ColorRgba32 ToNativeColor(TColor fc);
    }
}
