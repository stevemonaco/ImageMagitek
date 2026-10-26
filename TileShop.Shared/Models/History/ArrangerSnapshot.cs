using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;
using ImageMagitek.Tilemaps;

namespace TileShop.Shared.Models;

/// <summary>
/// Copy of an arranger's elements and each element's palette at a point in time, or of a tilemap view's entry grid
/// </summary>
/// <remarks>
/// Palettes are stored separately as a defense against codec instances that clones share.
/// </remarks>
public sealed class ArrangerSnapshot
{
    private readonly Arranger? _arranger;
    private readonly Palette?[,]? _palettes;
    private readonly TilemapView? _tilemapView;

    private ArrangerSnapshot(Arranger arranger, Palette?[,] palettes)
    {
        _arranger = arranger;
        _palettes = palettes;
    }

    private ArrangerSnapshot(TilemapView tilemapView) => _tilemapView = tilemapView;

    public static ArrangerSnapshot Capture(IArrangerView arranger)
    {
        if (arranger is TilemapView view)
            return new ArrangerSnapshot(view.WithGrid(view.Grid.Clone()));

        var clone = ((Arranger)arranger).CloneArranger();
        var size = clone.ArrangerElementSize;
        var palettes = new Palette?[size.Height, size.Width];

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                if (clone.GetElement(x, y)?.Codec is IIndexedCodec codec)
                    palettes[y, x] = codec.Palette;
            }
        }

        return new ArrangerSnapshot(clone, palettes);
    }

    /// <summary>
    /// Creates a new arranger matching the snapshot. Elements whose palette changed since capture get a cloned codec,
    /// so codecs shared with other arrangers are never modified. A tilemap view is restored on a copy of its grid.
    /// </summary>
    public IArrangerView Restore(ICodecFactory codecFactory)
    {
        if (_tilemapView is not null)
            return _tilemapView.WithGrid(_tilemapView.Grid.Clone());

        var arranger = _arranger!.CloneArranger();
        var size = arranger.ArrangerElementSize;

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                if (arranger.GetElement(x, y) is not { Codec: IIndexedCodec codec } element
                    || _palettes![y, x] is not { } palette || ReferenceEquals(codec.Palette, palette))
                    continue;

                var clone = (IIndexedCodec)codecFactory.CloneCodec(codec);
                clone.Palette = palette;
                arranger.SetElement(element.WithCodec(clone), x, y);
            }
        }

        return arranger;
    }
}
