namespace ImageMagitek.Codec
{
    public class ImageProperty
    {
        public int ColorDepth { get; set; }
        public bool RowInterlace { get; set; }

        /// <summary>
        /// Original placement pattern of pixels as specified by the codec
        /// </summary>
        public int[] RowPixelPattern { get; private set; }

        /// <summary>
        /// Placement pattern of pixels extended to the width of the element
        /// </summary>
        public int[] RowExtendedPixelPattern { get; set; }

        public ImageProperty(int colorDepth, bool rowInterlace, int[] rowPixelPattern)
        {
            ColorDepth = colorDepth;
            RowInterlace = rowInterlace;
            RowPixelPattern = rowPixelPattern;
            RowExtendedPixelPattern = RowPixelPattern;
        }

        /// <summary>
        /// Extends the RowPixelPattern to the required image width
        /// </summary>
        /// <param name="width">The width of the ArrangerElement to be decoded</param>
        public void ExtendRowPattern(int width)
        {
            if (RowExtendedPixelPattern.Length >= width) // Previously sized to be sufficiently large
                return;

            RowExtendedPixelPattern = new int[width];

            int patternIndex = 0; // Index into RowPixelPattern
            int extendedIndex = 0;   // Index into RowExtendedPixelPattern
            int offset = 0;

            while (extendedIndex < width)
            {
                RowExtendedPixelPattern[extendedIndex] = offset + RowPixelPattern[patternIndex];
                extendedIndex++;
                patternIndex++;

                if(patternIndex >= RowPixelPattern.Length)
                {
                    patternIndex = 0;
                    offset += RowPixelPattern.Length;
                }
            }
        }
    }
}
