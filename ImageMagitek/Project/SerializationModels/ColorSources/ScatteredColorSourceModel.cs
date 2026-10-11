using System.Collections.Generic;
using System.Linq;
using ImageMagitek.Colors;

namespace ImageMagitek.Project.Serialization;

/// <summary>
/// A run of scattered colors, stored as one <c>scatteredcolor</c> element
/// </summary>
public class ScatteredColorSourceModel : IColorSourceModel
{
    public int Entries { get; set; }
    public List<ColorFieldRun> Fields { get; set; }

    public ScatteredColorSourceModel(int entries, List<ColorFieldRun> fields)
    {
        Entries = entries;
        Fields = fields;
    }

    public bool ResourceEquals(IColorSourceModel sourceModel)
    {
        return sourceModel is ScatteredColorSourceModel model && model.Entries == Entries && model.Fields.SequenceEqual(Fields);
    }
}
