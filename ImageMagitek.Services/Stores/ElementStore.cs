using System.Collections.Generic;

namespace ImageMagitek.Services.Stores;
public sealed class ElementStore
{
    public TileLayout DefaultElementLayout { get; set; } = TileLayout.Default;
    public Dictionary<string, TileLayout> ElementLayouts { get; } = new();
}
