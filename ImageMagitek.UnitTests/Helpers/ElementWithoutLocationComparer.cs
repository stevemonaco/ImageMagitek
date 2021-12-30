using System.Collections;

namespace ImageMagitek.UnitTests.Helpers;

public class ElementWithoutLocationComparer : IComparer
{
    public int Compare(object a, object b)
    {
        if (a is ArrangerElement elA && b is ArrangerElement elB)
        {
            if (elA.Height != elA.Height)
                return -1;

            if (elA.Width != elB.Width)
                return -1;

            if (elA.Codec.Name != elB.Codec.Name)
                return -1;

            if (elA.SourceAddress != elB.SourceAddress)
                return -1;

            if (elA.Source.Name != elB.Source.Name)
                return -1;

            if (elA.Palette?.Name != elB.Palette?.Name)
                return -1;

            return 0;
        }

        return -1;
    }
}
