using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

public class ProjectForeignColorSourceModel : IColorSourceModel
{
    public IColor Value { get; set; }

    public ProjectForeignColorSourceModel(IColor value)
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
