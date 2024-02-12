using System.Collections;
using ImageMagitek.Codec;

namespace ImageMagitek.UnitTests.Helpers;
public class ElementWithoutLocationComparer : IComparer
{
    public int Compare(object? a, object? b)
    {
        if (a is ArrangerElement elA && b is ArrangerElement elB)
        {
            if (elA.Width != elB.Width)
                return elA.Width.CompareTo(elB.Width);

            if (elA.Height != elB.Height)
                return elA.Height.CompareTo(elB.Height);

            if (elA.Codec.Name != elB.Codec.Name)
                return elA.Codec.Name.CompareTo(elB.Codec.Name);

            if (elA.SourceAddress.BitOffset != elB.SourceAddress.BitOffset)
                return elA.SourceAddress.BitOffset.CompareTo(elB.SourceAddress.BitOffset);

            if (elA.Source.Name != elB.Source.Name)
                return elA.Source.Name.CompareTo(elB.Source.Name);

            if (elA.Codec is IIndexedCodec codecA && elB.Codec is IIndexedCodec codecB)
            {
                if (codecA.Palette.Name != codecB.Palette.Name)
                    return codecA.Palette.Name.CompareTo(codecB.Palette.Name);
            }

            return 0;
        }

        return -1;
    }
}
