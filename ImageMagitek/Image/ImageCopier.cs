using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace ImageMagitek.Image
{
    public static class ImageCopier
    {
        public static bool CanCopy(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight, bool useExactRemap)
        {
            return true;
        }

        public static void Copy(IndexedImage source, IndexedImage dest, Point sourceStart, Point destStart, int copyWidth, int copyHeight, bool useExactRemap)
        {
            if (!CanCopy(source, dest, sourceStart, destStart, copyWidth, copyHeight, useExactRemap))
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

        public static void Copy(IndexedImage source, DirectImage dest, Rectangle sourceRect, Rectangle destRect)
        {

        }

        public static void Copy(DirectImage source, IndexedImage dest, Rectangle sourceRect, Rectangle destRect, bool useExactRemap)
        {

        }

        public static void Copy(DirectImage source, DirectImage dest, Rectangle sourceRect, Rectangle destRect)
        {

        }
    }
}
