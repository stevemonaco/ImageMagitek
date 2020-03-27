using ImageMagitek;
using ImageMagitek.Colors;

namespace TileShop.Shared.Models
{
    public class ArrangerTransferModel
    {
        /// <summary>
        /// Arranger to be copied from
        /// </summary>
        public Arranger Arranger { get; set; }

        /// <summary>
        /// Arranger to be pasted unto
        /// </summary>
        public Arranger DestinationArranger { get; set; }

        /// <summary>
        /// Left edge of arranger subsection in pixels
        /// </summary>
        public int X { get; set; }
        /// <summary>
        /// Top edge of arranger subsection in pixels
        /// </summary>
        public int Y { get; set; }
        /// <summary>
        /// Width of arranger subsection in pixels
        /// </summary>
        public int Width { get; set; }
        /// <summary>
        /// Height of arranger subsection in pixels
        /// </summary>
        public int Height { get; set; }

        public ArrangerTransferModel() { }

        /// <summary>
        /// Model to transfer a subsection of an Arranger
        /// </summary>
        /// <param name="arranger">Arranger to be copied from</param>
        /// <param name="x">Left edge of arranger subsection in pixels</param>
        /// <param name="y">Top edge of arranger subsection in pixels</param>
        /// <param name="width">Width of arranger subsection in pixels</param>
        /// <param name="height">Height of arranger subsection in pixels</param>
        public ArrangerTransferModel(Arranger arranger, int x, int y, int width, int height)
        {
            Arranger = arranger;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    public class IndexedImageTransferModel : ArrangerTransferModel
    {
        public byte[,] Image { get; set; }

        public IndexedImageTransferModel(Arranger arranger, int x, int y, int width, int height)
        {
            Arranger = arranger;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    public class DirectImageTransferModel : ArrangerTransferModel
    {
        public ColorRgba32[,] Image { get; set; }

        public DirectImageTransferModel(Arranger arranger, int x, int y, int width, int height)
        {
            Arranger = arranger;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    public class ElementTransferModel : ArrangerTransferModel
    {
        public ArrangerElement[,] Image { get; set; }

        public ElementTransferModel(Arranger arranger, int x, int y, int width, int height)
        {
            Arranger = arranger;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
