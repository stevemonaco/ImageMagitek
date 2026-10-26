using System.Drawing;
using ImageMagitek.Codec;

namespace ImageMagitek.Tilemaps;

/// <summary>
/// A read-only view of a tilemap's working entry grid that an editor draws and writes pixels through.
/// Its cells are built the same way as the tilemap's own, so equal entries render equally, but with this view's own slot codecs.
/// </summary>
public sealed class TilemapView : IArrangerView
{
    private readonly IGraphicsCodec[] _slotCodecs;
    private ArrangerElement?[,]? _elements;
    private int _builtVersion;

    public TilemapArranger Tilemap { get; }
    public TilemapGrid Grid { get; }

    public string Name => Tilemap.Name;
    public Size ArrangerElementSize => Tilemap.ArrangerElementSize;
    public Size ElementPixelSize => Tilemap.ElementPixelSize;
    public Size ArrangerPixelSize => Tilemap.ArrangerPixelSize;
    public ElementLayout Layout => ElementLayout.Tiled;
    public PixelColorType ColorType => PixelColorType.Indexed;

    internal TilemapView(TilemapArranger tilemap, TilemapGrid grid, IGraphicsCodec[] slotCodecs)
    {
        Tilemap = tilemap;
        Grid = grid;
        _slotCodecs = slotCodecs;
    }

    public ArrangerElement? GetElement(int elemX, int elemY)
    {
        if (_elements is null || _builtVersion != Grid.Version)
        {
            _elements = Tilemap.CreateElements(Grid, _slotCodecs);
            _builtVersion = Grid.Version;
        }

        return _elements[elemY, elemX];
    }

    /// <summary>
    /// A view of <paramref name="grid"/> sharing this view's slot codecs, so history snapshots keep one codec set per editor
    /// </summary>
    public TilemapView WithGrid(TilemapGrid grid) => new(Tilemap, grid, _slotCodecs);
}
