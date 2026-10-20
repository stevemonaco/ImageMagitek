using System;
using System.Collections.Generic;
using System.Drawing;
using ImageMagitek.Codec;
using ImageMagitek.Project;

namespace ImageMagitek.Metatiles;

/// <summary>
/// One metatile layer of a <see cref="MetatileMap"/> as a grid of tiles, derived from the block map and the metatile tables; its elements cannot be set.
/// </summary>
public sealed class MetatileLayerArranger : Arranger
{
    private readonly MetatileMap _map;

    public int LayerIndex { get; }

    /// <inheritdoc/>
    protected override ArrangerElement?[,] ElementGrid
    {
        get => _map.GetLayerGrid(LayerIndex);
        set => throw new InvalidOperationException($"Layer {LayerIndex + 1} of metatile map '{_map.Name}' derives its elements from the block map");
    }

    /// <inheritdoc/>
    public override string Name
    {
        get => $"{_map.Name} Layer {LayerIndex + 1}";
        set => throw new InvalidOperationException($"Layer {LayerIndex + 1} of metatile map '{_map.Name}' is named after the map");
    }

    /// <inheritdoc/>
    public override bool ShouldBeSerialized { get; set; }

    internal MetatileLayerArranger(MetatileMap map, int layerIndex)
    {
        _map = map;
        LayerIndex = layerIndex;
        Mode = ArrangerMode.Tilemap;
        Layout = ElementLayout.Tiled;
        ColorType = PixelColorType.Indexed;
        ElementPixelSize = map.TileSize;
        ArrangerElementSize = new Size(map.Width * map.Shape.Width, map.Height * map.Shape.Height);
    }

    /// <exception cref="InvalidOperationException">Always; a metatile map's size comes from its resource definition</exception>
    public override void Resize(int arrangerWidth, int arrangerHeight) =>
        throw new InvalidOperationException($"Metatile map '{_map.Name}' cannot be resized");

    /// <exception cref="InvalidOperationException">Always; the elements follow from the block map and metatile tables</exception>
    public override void SetElement(in ArrangerElement? element, int elemX, int elemY) =>
        throw new InvalidOperationException($"Metatile map '{_map.Name}' derives its elements from its block map and metatile tables");

    /// <exception cref="InvalidOperationException">Always; the elements follow from the block map and metatile tables</exception>
    public override void ResetElement(int elemX, int elemY) =>
        throw new InvalidOperationException($"Metatile map '{_map.Name}' derives its elements from its block map and metatile tables");

    /// <exception cref="InvalidOperationException">Always; edits work on block numbers, not cloned elements</exception>
    protected override Arranger CloneArrangerCore(int posX, int posY, int width, int height) =>
        throw new InvalidOperationException($"Metatile map '{_map.Name}' cannot be cloned");

    /// <summary>
    /// The map's linked resources
    /// </summary>
    public override IEnumerable<IProjectResource> LinkedResources => _map.LinkedResources;
}
