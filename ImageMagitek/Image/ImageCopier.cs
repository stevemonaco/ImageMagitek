using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace ImageMagitek.Image
{
    public static class ImageCopier
    {
        public static bool CanCopyPixels(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight, bool useIndexExactRemap)
        {
            if (copyWidth > (source.Width - sourceStart.X))
                return false;
            if (copyHeight > (source.Height - sourceStart.Y))
                return false;

            if (copyWidth > (dest.Width - destStart.X))
                return false;
            if (copyHeight > (dest.Height - destStart.Y))
                return false;

            if (useIndexExactRemap)
                return true;

            // Check if color remap will succeed

            return true;
        }

        public static void CopyPixels(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight, bool useExactIndexRemap)
        {
            if (!CanCopyPixels(source, dest, sourceStart, destStart, copyWidth, copyHeight, useExactIndexRemap))
                return;

            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    var index = source.GetPixel(x + sourceStart.X, y + sourceStart.Y);
                    dest.SetPixel(x + destStart.X, y + destStart.Y, index);
                }
            }
        }

        public static void CopyPixels(IndexedImage source, DirectImage dest, Rectangle sourceRect, Rectangle destRect)
        {

        }

        public static void CopyPixels(DirectImage source, IndexedImage dest, Rectangle sourceRect, Rectangle destRect, bool useExactRemap)
        {

        }

        public static void CopyPixels(DirectImage source, DirectImage dest, Rectangle sourceRect, Rectangle destRect)
        {

        }
    }
}
