using System.Collections.Generic;

namespace ImageMagitek.Codec
{
    public sealed class ImageProperty
    {
        public int ColorDepth { get; set; }
        public bool RowInterlace { get; set; }

        /// <summary>
        /// Placement pattern of pixels within a row
        /// </summary>
        public RepeatList RowPixelPattern { get; private set; }

        public ImageProperty(int colorDepth, bool rowInterlace, IEnumerable<int> rowPixelPattern)
        {
            ColorDepth = colorDepth;
            RowInterlace = rowInterlace;
            RowPixelPattern = new RepeatList(rowPixelPattern);
        }
    }
}
