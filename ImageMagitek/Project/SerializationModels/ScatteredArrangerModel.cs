using System.Drawing;
using System.Linq;

namespace ImageMagitek.Project.Serialization;

public class ScatteredArrangerModel : ResourceModel
{
    public required ArrangerElementModel[,] ElementGrid { get; init; }
    public Size ArrangerElementSize { get; set; }
    public Size ElementPixelSize { get; set; }
    public ElementLayout Layout { get; set; }
    public PixelColorType ColorType { get; set; }

    public override bool ResourceEquals(ResourceModel resourceModel)
    {
        if (resourceModel is not ScatteredArrangerModel model)
            return false;

        if (model.ArrangerElementSize != ArrangerElementSize || model.ElementPixelSize != ElementPixelSize ||
            model.Layout != Layout || model.ColorType != ColorType)
            return false;

        return model.EnumerateElements()
            .Zip(this.EnumerateElements())
            .All(x => x.First?.ResourceEquals(x.Second) ?? (x.Second is null));
    }
}
