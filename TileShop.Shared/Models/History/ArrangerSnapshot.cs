using ImageMagitek;
using ImageMagitek.Codec;
using ImageMagitek.Colors;

namespace TileShop.Shared.Models;

/// <summary>
/// Copy of an arranger's elements and each element's palette at a point in time
/// </summary>
/// <remarks>
/// Palettes are stored separately because cloned arrangers share codec instances, and applying a palette changes the codec in place.
/// </remarks>
public sealed class ArrangerSnapshot
{
    private readonly Arranger _arranger;
    private readonly Palette?[,] _palettes;

    private ArrangerSnapshot(Arranger arranger, Palette?[,] palettes)
    {
        _arranger = arranger;
        _palettes = palettes;
    }

    public static ArrangerSnapshot Capture(Arranger arranger)
    {
        var clone = arranger.CloneArranger();
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
    /// so codecs shared with other arrangers are never modified.
    /// </summary>
    public Arranger Restore(ICodecFactory codecFactory)
    {
        var arranger = _arranger.CloneArranger();
        var size = arranger.ArrangerElementSize;

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                if (arranger.GetElement(x, y) is not { Codec: IIndexedCodec codec } element
                    || _palettes[y, x] is not { } palette || ReferenceEquals(codec.Palette, palette))
                    continue;

                var clone = (IIndexedCodec)codecFactory.CloneCodec(codec);
                clone.Palette = palette;
                arranger.SetElement(element.WithCodec(clone), x, y);
            }
        }

        return arranger;
    }
}
