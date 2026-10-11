using System.Collections.Generic;
using ImageMagitek.Colors;

namespace TileShop.Shared.Models;

/// <summary>
/// A run of scattered colors, edited as a whole through the Scattered Colors dialog
/// </summary>
public class ScatteredColorSourceModel : ColorSourceModel
{
    public int Entries { get; }
    public IReadOnlyList<ColorFieldRun> Fields { get; }

    public string Summary => $"{Entries} {(Entries == 1 ? "color" : "colors")} · {Fields.Count} {(Fields.Count == 1 ? "field" : "fields")}";

    public ScatteredColorSourceModel(int entries, IReadOnlyList<ColorFieldRun> fields)
    {
        Entries = entries;
        Fields = fields;
    }
}
