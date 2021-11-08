using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

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

            if (elA.FileAddress != elB.FileAddress)
                return -1;

            if (elA.DataFile.Name != elB.DataFile.Name)
                return -1;

            if (elA.Palette?.Name != elB.Palette?.Name)
                return -1;

            return 0;
        }

        return -1;
    }
}
