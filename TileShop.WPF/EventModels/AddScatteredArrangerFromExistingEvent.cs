using ImageMagitek;

namespace TileShop.WPF.EventModels
{
    public class AddScatteredArrangerFromExistingEvent
    {
        public Arranger Arranger { get; set; }
        public int ElementX { get; set; }
        public int ElementY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// Event to create a new scattered arranger from an existing arranger
        /// </summary>
        /// <param name="arranger">Arranger to have elements copied from</param>
        /// <param name="elementX">X-coordinate of starting element in element coordinates</param>
        /// <param name="elementY">Y-coordinate of starting element in element coordinates</param>
        /// <param name="width">Width of arranger in elements</param>
        /// <param name="height">Height of arranger in elements</param>
        public AddScatteredArrangerFromExistingEvent(Arranger arranger, int elementX, int elementY, int width, int height)
        {
            Arranger = arranger;
            ElementX = elementX;
            ElementY = elementY;
            Width = width;
            Height = height;
        }
    }
}
