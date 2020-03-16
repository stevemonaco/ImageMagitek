  using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace ImageMagitek
{
    public class ElementCopier
    {
        public static bool CanCopyElements(Arranger source, Arranger dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
        {
            if (copyWidth > (source.ArrangerElementSize.Width - sourceStart.X))
                return false;
            if (copyHeight > (source.ArrangerElementSize.Height - sourceStart.Y))
                return false;

            if (copyWidth > (dest.ArrangerElementSize.Width - destStart.X))
                return false;
            if (copyHeight > (dest.ArrangerElementSize.Height - destStart.Y))
                return false;

            if (source.ElementPixelSize != dest.ElementPixelSize)
                return false;

            if (dest.Layout == ArrangerLayout.Single)
                return false;

            if (source.ColorType != dest.ColorType)
                return false;

            return true;
        }

        public static void CopyElements(Arranger source, ScatteredArranger dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight)
        {
            if (!CanCopyElements(source, dest, sourceStart, destStart, copyWidth, copyHeight))
                return;

            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    var el = source.GetElement(x + sourceStart.X, y + sourceStart.Y);
                    dest.SetElement(el, x + destStart.X, y + destStart.Y);
                }
            }
        }
    }
}
