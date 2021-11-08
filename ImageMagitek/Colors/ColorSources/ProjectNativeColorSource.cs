namespace ImageMagitek.Colors;

public class ProjectNativeColorSource : IColorSource
{
    public ColorRgba32 Value { get; set; }

    public ProjectNativeColorSource(ColorRgba32 value)
    {
        Value = value;
    }
}
