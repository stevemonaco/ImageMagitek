using System.Collections.Generic;
using System.Linq;
using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Services.Stores;

namespace TileShop.UI.Features.Graphics;

/// <summary>
/// Builds a graphics editor's palette list and chooses the palette it selects.
/// </summary>
public sealed class EditorPaletteList
{
    private readonly PaletteStore _paletteStore;

    public EditorPaletteList(PaletteStore paletteStore)
    {
        _paletteStore = paletteStore;
    }

    public IReadOnlyList<Palette> Build(IArrangerView arranger)
    {
        if (arranger is SequentialArranger sequential)
            return _paletteStore.GlobalPalettes.Where(sequential.CanUsePalette).ToList();

        var arrangerPalettes = arranger.GetReferencedPalettes();
        arrangerPalettes.ExceptWith(_paletteStore.GlobalPalettes);

        return arrangerPalettes.OrderBy(x => x.Name)
            .Concat(_paletteStore.GlobalPalettes.OrderBy(x => x.Name))
            .ToList();
    }

    public Palette? Select(IArrangerView arranger, IReadOnlyList<Palette> palettes)
    {
        if (arranger is SequentialArranger { ActivePalette: { } active } && palettes.Contains(active))
            return active;

        return palettes.FirstOrDefault();
    }
}
