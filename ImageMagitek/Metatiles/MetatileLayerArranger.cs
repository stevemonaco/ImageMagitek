using System.Drawing;

namespace ImageMagitek.Metatiles;

/// <summary>
/// One metatile layer of a <see cref="MetatileMap"/> as a read-only view of tiles, derived from the block map and the metatile tables
/// </summary>
public sealed class MetatileLayerArranger : IArrangerView
{
    private readonly MetatileMap _map;

    public int LayerIndex { get; }

    public string Name => $"{_map.Name} Layer {LayerIndex + 1}";
    public Size ArrangerElementSize { get; }
    public Size ElementPixelSize { get; }

    public Size ArrangerPixelSize =>
        new(ArrangerElementSize.Width * ElementPixelSize.Width, ArrangerElementSize.Height * ElementPixelSize.Height);

    public ElementLayout Layout => ElementLayout.Tiled;
    public PixelColorType ColorType => PixelColorType.Indexed;

    internal MetatileLayerArranger(MetatileMap map, int layerIndex)
    {
        _map = map;
        LayerIndex = layerIndex;
        ElementPixelSize = map.TileSize;
        ArrangerElementSize = new Size(map.Width * map.Shape.Width, map.Height * map.Shape.Height);
    }

    public ArrangerElement? GetElement(int elemX, int elemY) => _map.GetLayerGrid(LayerIndex)[elemY, elemX];
}
