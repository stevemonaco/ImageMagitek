using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

public class ProjectNativeColorSourceModel : IColorSourceModel
{
    public ColorRgba32 Value { get; set; }

    public ProjectNativeColorSourceModel(ColorRgba32 value)
    {
        Value = value;
    }

    public bool ResourceEquals(IColorSourceModel sourceModel)
    {
        if (sourceModel is not ProjectNativeColorSourceModel model)
            return false;

        return Value.Color == model.Value.Color;
    }
}
